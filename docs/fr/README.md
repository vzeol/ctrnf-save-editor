# CTR Nitro-Fueled — Éditeur de sauvegardes

[English](../../README.md) · **Français**

Un éditeur communautaire de **sauvegardes PS4 déchiffrées**, avec une interface Windows en français et en anglais et un outil en ligne de commande.

La version **1.0** reprend les fonctionnalités actuelles. Certaines correspondances restent expérimentales et à confirmer en jeu. Les corrections et améliorations suivront via les issues, les pull requests et les prochaines versions.

**Participez au projet !** Un personnage, une traduction ou une condition manque ? [Ouvrez une issue](https://github.com/vzeol/ctrnf-save-editor/issues/new/choose) ou proposez une [pull request](../../CONTRIBUTING.md). Les observations contrôlées avant/après en jeu nous aident à confirmer les correspondances. N'envoyez pas votre sauvegarde personnelle dans une issue publique.

## Démarrer

1. Téléchargez le ZIP Windows x64 dans les [Releases](https://github.com/vzeol/ctrnf-save-editor/releases) et extrayez-le entièrement. Le runtime .NET est inclus.
2. Fermez le jeu et conservez une copie indépendante de vos sauvegardes.
3. Lancez `ctrnf-save-gui.exe`. Choisissez le type de sauvegarde, puis utilisez **Enregistrer sous** pour votre premier essai.

Les chemins suivants partent du **dossier de votre propre installation** :

| Bouton | Chemin relatif | Contenu |
|---|---|---|
| Ouvrir une save Aventure | `user\savedata\1\CUSA13795\savedata1…4\SYS-DATA.sav` | Progression d'un emplacement : trophées, jetons, reliques, gemmes, boss et difficulté |
| Ouvrir le profil global | `user\savedata\1\CUSA13795\savedata0\SYS-DATA.sav` | Personnages, skins, personnalisation, pièces Wumpa, œufs et contre-la-montre communs aux emplacements |

Le dossier utilisateur et l'identifiant du jeu peuvent varier. L'éditeur ne déchiffre pas les sauvegardes console. L'inventaire du serveur et celui de la sauvegarde locale sont distincts ; une modification locale ne modifie pas votre compte serveur.

## Garage

Dans **Configuration actuelle**, choisis le pilote, l’apparence, le châssis,
les roues, la peinture et l’autocollant, puis **Appliquer les modifications**.
Changer de pilote remet l’apparence à 0 ; **Réinitialiser** annule les choix en
attente. Les dix karts spéciaux (huit karts rétro, Faucon de feu, Kart Xfinity Flash) appliquent
leur configuration complète et désactivent roues/peinture/autocollant jusqu’au choix d’un châssis
normal. Les autres karts spéciaux restent indisponibles sans preuve pour chaque
mot. La CLI propose aussi `garage set` pour modifier les pièces.

Dans un profil global, ouvre **Garage** pour mémoriser la configuration actuelle
sous un nom, l’appliquer, la renommer, la supprimer ou importer/exporter un JSON.
Les fichiers sont conservés dans `%LOCALAPPDATA%\ctrnf-save-editor\garage\`.
Le Garage affiche les noms du jeu en français ou en anglais pour le pilote,
l’apparence, le châssis, les roues, la peinture et l’autocollant ; les valeurs
inconnues restent visibles. La commande `garage show <global.sav> --lang fr`
affiche également les noms français et conserve les valeurs brutes dans le JSON.
Appliquer modifie le profil en mémoire ; **Enregistrer** écrit ensuite le fichier
avec le backup habituel. Les réglages des karts spéciaux sont conservés ensemble.
Un pilote verrouillé connu affiche un avertissement ; la possession du châssis et
des autres pièces n’est pas vérifiée. Voir le [guide](../usage.md#garage) et les
[limites du format](../format/kart-loadout.md) en anglais.

## Fonctionnalités et limites

- **Aventure** : récompenses par circuit, gemmes, clés, cohérence et cascades, deux victoires d'Oxide distinctes et difficulté. Les principales règles ont été testées en jeu ; certaines dépendances des boss restent déduites.
- **Profil global** : pièces Wumpa, cinq œufs identifiés, catalogue de 56 pilotes et 392 skins, conditions connues de personnalisation et compteurs de victoires partagés entre skins.
- **Tout débloquer / Tout verrouiller** : aperçu des changements avant application, avec conservation du contenu de base et des données sans rapport. Les conditions inconnues restent en lecture seule.
- **Contre-la-montre** : progression des fantômes et records existants. Aucune création d'entrée de circuit manquante ni de trajectoire de fantôme.
- **Champion** : conditions natives identifiées pour le châssis, les roues, la peinture et certaines récompenses de fantômes. La validation en jeu de l'ensemble du catalogue reste à faire.

Les 512 bits historiques sont des **défis**, pas 512 personnages. Les modifications restent en mémoire jusqu'à l'enregistrement. L'éditeur crée des backups et refuse les changements externes détectés ; gardez aussi votre propre copie. Le jeu peut réattribuer une récompense si ses conditions restent remplies.

## Développement et participation

Le dépôt est organisé en moteur indépendant, CLI, interface WinForms et tests synthétiques publics. Le SDK .NET 10 et Windows sont nécessaires pour compiler toute la solution. Aucun jeu, sauvegarde personnelle ou artwork du jeu n'est fourni.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/test.ps1
pwsh ./scripts/publish.ps1
```

Voir [l'architecture](../architecture.md), [les contributions](../../CONTRIBUTING.md), [la feuille de route](../../ROADMAP.md) et [le guide d'utilisation](../usage.md) en anglais. Issues et PR en français bienvenues ; l'anglais reste la langue principale du dépôt.

## Licence

**Crédits pour la GUI :** merci à **Ryu7w7 et aux contributeurs d'[OCTR Nitro-Fueled PC](https://github.com/Ryu7w7/OCTR-NitroFueled-PC)**. Leur launcher a servi de référence visuelle pour la palette bleue, le bandeau et la navigation. L'interface WinForms de cet éditeur est une implémentation indépendante.

Ce travail a été possible grâce à toute cette communauté. Les **[crédits complets](../../CREDITS.md)** citent les personnes et projets remerciés par OCTR, de ses fondations techniques aux patches, mods et contributions aux tests.

Le code et la documentation du projet sont sous [PolyForm Noncommercial 1.0.0](../../LICENSE.md). Lecture, modification et partage sont autorisés selon ses termes pour un usage non commercial. L'exploitation commerciale n'est pas accordée : il s'agit de code accessible sous licence non commerciale, pas d'une licence open source OSI.

La police Lilita One possède sa licence SIL OFL séparée. [Crédits et notices](../../THIRD_PARTY_NOTICES.md). L'interface s'inspire visuellement du launcher [OCTR Nitro-Fueled PC](https://github.com/Ryu7w7/OCTR-NitroFueled-PC). Projet communautaire non officiel, sans affiliation aux ayants droit du jeu.

## À propos et mises à jour

Ouvre **À propos** depuis la barre d’outils ou l’accueil pour consulter la version, les crédits et les liens du projet. La vérification facultative au démarrage contacte **api.github.com uniquement** par une requête GET, avec la version de l’éditeur comme User-Agent. Désactive-la dans À propos ; la vérification manuelle reste disponible. Seules les préférences `checkUpdatesAtStartup` et `skippedVersion` sont enregistrées dans `%LOCALAPPDATA%\ctrnf-save-editor\settings.json`. Aucune sauvegarde n’est envoyée.

Une version extraite et accessible en écriture peut télécharger le ZIP et son SHA-256 depuis GitHub, les vérifier, puis installer et redémarrer après enregistrement ou abandon des modifications. Le téléchargement peut être annulé. Les builds de développement proposent la page de version. L’installation précédente est copiée dans `%LOCALAPPDATA%\ctrnf-save-editor\previous\<ancienne-version>\`. Pour revenir en arrière, ferme l’éditeur et recopie le contenu de ce dossier dans le dossier d’installation. Les sauvegardes ne sont jamais modifiées par la mise à jour. Si la copie échoue, la version précédente est restaurée et relancée automatiquement. Les erreurs d’installation sont consignées dans `%LOCALAPPDATA%\ctrnf-save-editor\updates\<version>\<exécution>\update.log` ; conserve la copie précédente pour la récupération.
