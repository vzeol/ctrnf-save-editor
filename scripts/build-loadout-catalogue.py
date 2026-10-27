"""Build factual Garage names from user-owned PS4 1.21 extracted data.

See docs/format/kart-loadout.md. Requires the external igz_file.py reader;
never writes input files and does not distribute the reader or game assets.
"""
import argparse
import importlib
import json
from pathlib import Path
import re
import struct
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--reader-dir', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.resolve().is_relative_to(args.input.resolve()):
        parser.error('Output must be outside the read-only input directory.')
    sys.dont_write_bytecode = True
    sys.path.insert(0, str(args.reader_dir))
    Igz = importlib.import_module('igz_file').Igz
    s = Igz((args.input / 'systemData.igz').read_bytes())
    types = dict(s.objects())

    def refs(z, offset):
        return [z.u64(z.u64(offset + 32) + i * 8) for i in range(z.u32(offset + 12))]

    def externals(z):
        f = z.fixup('EXNM')
        return [(z.strings[a & 0x7fffffff], z.strings[b & 0x7fffffff], bool(b & 0x80000000))
                for a, b in (struct.unpack_from('<II', f[3], i * 8) for i in range(f[1]))]

    # ONAM is the named-root directory; its entries pair with the root object list.
    name_list = next(o for o, t in s.objects() if t == 'igNameList')
    names = {s.string_at(s.u64(name_list + 32) + i * 16): o
             for i, o in enumerate(refs(s, 0))}
    assert len(names) == s.u32(name_list + 12)
    reverse = {}
    for name, offset in names.items():
        reverse.setdefault(offset, []).append(name)

    localized = {}
    for language in ('en', 'fr'):
        labels = {}
        for stem in ('MarketplaceData', 'systemData'):
            z = Igz((args.input / f'{stem}_{language}.lng').read_bytes())
            ex = externals(z)
            owners = [name for name, namespace, _ in ex if namespace != 'metafield']
            fields = [name for name, namespace, _ in ex if namespace == 'metafield']
            for offset, kind in z.objects():
                if kind == 'igLocalizedStringData':
                    owner = owners[z.u64(offset + 24) & 0x7fffffff]
                    field = fields[z.u64(offset + 32) & 0x7fffffff]
                    labels[owner, field] = z.string_at(offset + 16)
        localized[language] = labels

    def row(index, offset, display=False):
        handles = reverse.get(offset, [])
        if len(handles) != 1:
            raise ValueError(f'Ambiguous named object at {offset:x}: {handles}')
        handle = handles[0]
        result = dict(index=index, handle=handle)
        for language, labels in localized.items():
            fields = (['CRacerBaseData::_displayName'] if display else []) + ['CMarketplaceItem::_storeDisplayName']
            # Missing translations remain null: the application displays the raw value.
            result['name' + language.title()] = next((labels[handle, f] for f in fields
                                                     if labels.get((handle, f))), None)
        return result

    result = dict(schemaVersion=1, source='PS4 1.21 SystemData and EN/FR localization')
    for category, kind in [('drivers', 'CRacerDriverData'), ('karts', 'CRacerKartData')]:
        rows = []
        list_kind = kind + 'List'
        available = next(o for o, t in s.objects() if t == list_kind and s.u32(o + 12) > 30)
        list_indices = {offset: i for i, offset in enumerate(refs(s, available)) if offset}
        for name, offset in names.items():
            match = re.fullmatch('SystemData_available' + category.title() + r'_(\d+)', name)
            if not match and not (types.get(offset) == kind and offset in list_indices):
                continue
            assert types[offset] == kind
            item = row(int(match[1]) if match else None, offset, True)
            item['listIndex'] = list_indices.get(offset)
            if category == 'drivers':
                item['driver'] = s.string_at(offset + 0x58)
                item['skins'] = [row(i, skin) for i, skin in enumerate(refs(s, s.u64(offset + 0x98)))]
            rows.append(item)
        result[category] = sorted(rows, key=lambda r: (r['index'] is None, r['index'] or 0, r['handle']))

    prefix = 'SystemData_kartCustomizationGlobalData_'
    lists = {}
    for category, suffix in [('wheels', 'kartWheelsCustomizationData'), ('paints', 'materialOverrideValuesList'),
                             ('stickers', 'stickers'), ('presets', 'customizationPresets')]:
        lists[category] = refs(s, names[prefix + suffix])
        result[category] = [row(i, offset) for i, offset in enumerate(lists[category])]

    # Authored preset handles establish complete compositions independently of save words 7/9.
    handles = [(n, ns) for n, ns, is_handle in externals(s) if is_handle]
    for item, offset in zip(result['presets'], lists['presets']):
        preset = s.u64(offset + 0x58)
        def target(delta):
            value = s.u64(preset + delta)
            if not value:
                return None
            name, namespace = handles[value & 0x7fffffff]
            assert namespace == 'SystemData'
            return name
        item['chassis'] = target(0x10)
        for category, delta, default in [('paints', 0x18, 0), ('wheels', 0x30, 0), ('stickers', 0x28, 0)]:
            name = target(delta)
            item[category] = next(r['index'] for r in result[category] if r['handle'] == name) if name else default
        item['decalHandle'] = target(0x20)

    # Only controlled captures establish the unknown save words; never infer them from list positions.
    for index, chassis, wheels, paint, word7, word9, label in [
            (0, 15, 9, 16, -1, 0, 'Blue Retro Kart'), (8, 32, 0, 99, 0, 8, 'Firehawk')]:
        item = next(p for p in result['presets'] if p['index'] == index)
        assert (item['chassis'], item['wheels'], item['paints'], item['stickers']) == (
            f'SystemData_availableKarts_{chassis}', wheels, paint, 0)
        item['word7'] = word7
        item['word9'] = word9
        item['evidence'] = f'Controlled in-game {label} capture; docs/format/kart-loadout.md'

    # Fail generation on independently supplied controlled observations; never override data to fit.
    def check(category, index, en, fr):
        item = next(r for r in result[category] if r['index'] == index)
        assert (item['nameEn'], item['nameFr']) == (en, fr), (category, index, item)
    check('drivers', 5, 'Komodo Joe', 'Komodo Joe')
    check('drivers', 58, 'Iron Checkpoint Crate', 'Caisse Point de contrôle')
    check('drivers', 48, 'King Chicken', 'Roi Poulet')
    check('karts', 0, 'Classic', 'Classique')
    check('karts', 7, 'Team Bandicoot', 'Team Bandicoot')
    check('karts', 32, 'Firehawk', 'Faucon de feu')
    check('presets', 0, 'Blue Retro Kart', 'Kart rétro bleu')
    check('wheels', 0, 'Classic', 'Classique')
    check('wheels', 15, 'Cabrio', 'Cabrio')
    check('paints', 0, 'Blue', 'Bleu')
    check('paints', 6, 'Purple', 'Violet')
    check('stickers', 0, 'No Sticker', "Pas d'autocollant")
    check('stickers', 57, 'Zero Black', 'Zéro noir')
    assert next(r for r in result['drivers'] if r['index'] == 5)['skins'][5]['nameFr'] == 'Komodo Joe Sorcier'
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Generated', ', '.join(f'{k}: {len(v)}' for k, v in result.items() if isinstance(v, list)))
    for category in ('drivers', 'karts', 'wheels', 'paints', 'stickers'):
        missing = [r['index'] for r in result[category] if not r['nameEn'] or not r['nameFr']]
        if missing:
            print(f'Unlocalized {category} (raw fallback): {missing}')


if __name__ == '__main__':
    main()
