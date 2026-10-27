using System.Globalization;
using System.Text.Json;
using CtrNfSave;

namespace CtrNfSave.Gui;

internal static class L
{
    private static readonly Dictionary<string, (string En, string Fr)> Strings = new()
    {
        ["garage"] = ("Garage", "Garage"),
        ["garageEditor"] = ("Current loadout", "Configuration actuelle"),
        ["garageDriver"] = ("Driver", "Pilote"),
        ["garageSkin"] = ("Skin", "Apparence"),
        ["garageChassis"] = ("Chassis / special kart", "Châssis / kart spécial"),
        ["garageWheels"] = ("Wheels", "Roues"),
        ["garagePaint"] = ("Paint", "Peinture"),
        ["garageSticker"] = ("Sticker", "Autocollant"),
        ["garageApplyChanges"] = ("Apply changes", "Appliquer les modifications"),
        ["garageReset"] = ("Reset", "Réinitialiser"),
        ["garageDriverOwned"] = ("Driver owned locally.", "Pilote possédé localement."),
        ["garageCurrent"] = ("Current loadout\r\nDriver: {0} · Skin: {1}\r\nChassis: {2}\r\nWheels: {3} · Paint: {4} · Sticker: {5}", "Configuration actuelle\r\nPilote : {0} · Apparence : {1}\r\nChâssis : {2}\r\nRoues : {3} · Peinture : {4} · Autocollant : {5}"),
        ["garagePresetDetails"] = ("Driver: {0} · Skin: {1}\r\nChassis: {2}\r\nWheels: {3} · Paint: {4} · Sticker: {5}", "Pilote : {0} · Apparence : {1}\r\nChâssis : {2}\r\nRoues : {3} · Peinture : {4} · Autocollant : {5}"),
        ["garageUnknownWords"] = ("Unknown words 7 / 9: {0} / {1}", "Mots inconnus 7 / 9 : {0} / {1}"),
        ["garageUnavailable"] = ("Garage unavailable: {0}", "Garage indisponible : {0}"),
        ["garageHint"] = ("Keep named kart presets here. Apply changes the open profile in memory; click Save to write it with the usual backup. Special kart settings are copied together.", "Conserve ici tes configurations nommées. Appliquer modifie le profil en mémoire ; clique sur Enregistrer pour écrire avec la sauvegarde habituelle. Les réglages des karts spéciaux sont copiés ensemble."),
        ["garageOwnershipUnknown"] = ("Chassis ownership is not checked. Skin and other parts ownership is not checked; server inventory is separate.", "La possession du châssis n’est pas vérifiée. Apparence et autres pièces non vérifiées ; inventaire serveur distinct."),
        ["garageDriverLocked"] = ("Warning: this preset’s driver is locked in this profile. Applying remains available.", "Attention : le pilote de cette configuration est verrouillé dans ce profil. L’application reste possible."),
        ["garageDriverUnknown"] = ("Driver ownership is not checked for this loadout index.", "La possession du pilote n’est pas vérifiée pour cet indice de configuration."),
        ["garageCapture"] = ("Save current as preset", "Mémoriser la configuration"),
        ["garageRename"] = ("Rename", "Renommer"),
        ["garageDelete"] = ("Delete", "Supprimer"),
        ["garageExport"] = ("Export JSON", "Exporter JSON"),
        ["garageImport"] = ("Import JSON", "Importer JSON"),
        ["garagePresetName"] = ("Preset name", "Nom de la configuration"),
        ["garageDeleteConfirm"] = ("Delete preset “{0}”?", "Supprimer la configuration « {0} » ?"),
        ["garageJsonFilter"] = ("Garage preset (*.json)|*.json", "Configuration Garage (*.json)|*.json"),
        ["garageInvalidFiles"] = ("{0} invalid or unreadable preset file(s) skipped.", "{0} fichier(s) de configuration invalide(s) ou illisible(s) ignoré(s)."),
        ["garageError"] = ("Garage: {0}", "Garage : {0}"),
        ["garageApplied"] = ("Kart preset applied in memory. Click Save to write the profile and create its backup.", "Configuration appliquée en mémoire. Clique sur Enregistrer pour écrire le profil et créer sa sauvegarde."),
        ["about"] = ("About", "À propos"),
        ["aboutDescription"] = ("A community editor for decrypted CTR Nitro-Fueled saves.", "Un éditeur communautaire de sauvegardes CTR Nitro-Fueled décryptées."),
        ["aboutCredits"] = ("Thanks to Ryu7w7 and the OCTR Nitro-Fueled PC contributors for the GUI inspiration. See CREDITS.md for full credits. Lilita One font: SIL Open Font License (OFL).", "Merci à Ryu7w7 et aux contributeurs OCTR Nitro-Fueled PC pour l’inspiration de l’interface. Crédits complets dans CREDITS.md. Police Lilita One : SIL Open Font License (OFL)."),
        ["github"] = ("GitHub repository", "Dépôt GitHub"),
        ["releases"] = ("Releases", "Versions"),
        ["reportIssue"] = ("Report an issue", "Signaler un problème"),
        ["credits"] = ("Credits", "Crédits"),
        ["startupUpdates"] = ("Check for updates at startup", "Vérifier les mises à jour au démarrage"),
        ["checkUpdates"] = ("Check for updates", "Vérifier les mises à jour"),
        ["checkingUpdates"] = ("Checking for updates…", "Recherche de mises à jour…"),
        ["updateAvailable"] = ("An update is available", "Une mise à jour est disponible"),
        ["upToDate"] = ("You are up to date.", "Tu disposes de la dernière version."),
        ["updateFailed"] = ("Could not check for updates. Check your connection and try again.", "Impossible de vérifier les mises à jour. Vérifie ta connexion et réessaie."),
        ["settingsFailed"] = ("Could not save update preferences.", "Impossible d’enregistrer les préférences de mise à jour."),
        ["browserFailed"] = ("Could not open the browser.", "Impossible d’ouvrir le navigateur."),
        ["installRestart"] = ("Install and restart", "Installer et redémarrer"),
        ["openRelease"] = ("Open release page", "Ouvrir la page de version"),
        ["later"] = ("Later", "Plus tard"),
        ["skipVersion"] = ("Skip this version", "Ignorer cette version"),
        ["releaseOnly"] = ("To update this build, download and extract a package from the release page.", "Pour mettre à jour cette installation, télécharge et extrais le ZIP depuis la page de version."),
        ["rollbackHint"] = ("The previous installation is kept in LocalAppData/ctrnf-save-editor/previous/<version> for rollback.", "L’installation précédente est conservée dans LocalAppData/ctrnf-save-editor/previous/<version> pour revenir en arrière."),
        ["preparingUpdate"] = ("Preparing update", "Préparation de la mise à jour"),
        ["downloadVerify"] = ("Downloading and verifying the release package…", "Téléchargement et vérification de la version…"),
        ["installFailed"] = ("Update preparation failed. No installation files were replaced. You can retry or use the release page.", "La préparation a échoué. Aucun fichier d’installation n’a été remplacé. Réessaie ou utilise la page de version."),
        ["characters"] = ("Characters", "Personnages"),
        ["openAdventure"] = ("Open Adventure save…", "Ouvrir une save Aventure…"),
        ["openGlobal"] = ("Open global profile…", "Ouvrir le profil global…"),
        ["adventureSaveDescription"] = ("Story progress: trophies, CTR tokens, relics, gems and bosses. One separate save per Adventure slot (1 to 4).", "Progression de l’histoire : trophées, jetons CTR, reliques, gemmes et boss. Une sauvegarde distincte par emplacement Aventure (1 à 4)."),
        ["globalSaveDescription"] = ("Shared across all Adventure slots: characters, skins, karts, customization, Wumpa coins, eggs and time attack.", "Commun à tous les emplacements Aventure : personnages, skins, karts, personnalisation, pièces Wumpa, œufs et contre-la-montre."),
        ["saveLocation"] = ("Path relative to your CTR game folder:", "Chemin depuis ton dossier du jeu CTR :"),
        ["normalSaveLocation"] = ("Without mods:", "Sans mods :"),
        ["modSaveLocation"] = ("With mods (persistent profile):", "Avec mods (profil persistant) :"),
        ["modSaveNotice"] = ("Mods use a separate save profile to protect your normal saves. Each mod launch creates a session copy; a normal game exit saves it back to the mod profile. Close the game before editing that profile. Normal and mod saves do not automatically sync with each other.", "Les mods utilisent un profil séparé pour protéger tes sauvegardes normales. Chaque lancement crée une copie de session, recopiée dans le profil des mods si le jeu se ferme normalement. Ferme le jeu avant de modifier ce profil. Les sauvegardes normales et celles des mods ne sont pas synchronisées automatiquement entre elles."),
        ["saveFileFilter"] = ("Decrypted saves (*.sav)|*.sav|All files (*.*)|*.*", "Sauvegardes décryptées (*.sav)|*.sav|Tous les fichiers (*.*)|*.*"),
        ["selectedAdventureInstead"] = ("This is an Adventure save. Use Open Adventure save. The global profile is savedata0\\SYS-DATA.sav.", "C’est une sauvegarde Aventure. Utilise Ouvrir une save Aventure. Le profil global est savedata0\\SYS-DATA.sav."),
        ["selectedGlobalInstead"] = ("This is the global profile. Use Open global profile. Adventure slots are savedata1 to savedata4.", "C’est le profil global. Utilise Ouvrir le profil global. Les emplacements Aventure sont savedata1 à savedata4."),
        ["search"] = ("Search by name…", "Rechercher par nom…"),
        ["searchTracks"] = ("Find a track or hub…", "Rechercher un circuit ou une zone…"),
        ["allCategories"] = ("All categories", "Toutes les catégories"),
        ["unlockVisibleItems"] = ("Unlock filtered items", "Débloquer les objets filtrés"),
        ["lockVisibleItems"] = ("Lock filtered items", "Verrouiller les objets filtrés"),
        ["unlockAll"] = ("Unlock all", "Tout débloquer"),
        ["lockAll"] = ("Lock all", "Tout verrouiller"),
        ["unlockDriverSkins"] = ("Unlock this driver's skins", "Débloquer les apparences du pilote"),
        ["lockDriverSkins"] = ("Lock this driver's skins", "Verrouiller les apparences du pilote"),
        ["bulkUnlockScope"] = ("All named characters, skins and editable customization. Base content and unknown conditions are preserved. Preview changes before applying; saving is a separate action.", "Tous les personnages nommés, apparences et personnalisations modifiables. Le contenu de base et les conditions inconnues sont conservés. Un aperçu précède l’application ; l’enregistrement reste une action séparée."),
        ["bulkUnlockPreview"] = ("{0} changes, including shared reward conditions and driver-win counters. Base content, unknown flags, coins, eggs, records and Adventure slots are preserved. The game can re-award rewards for challenges already completed. Apply?", "{0} changements, dont les conditions partagées et les compteurs de victoires des pilotes. Le contenu de base, les flags inconnus, pièces, œufs, records et emplacements Aventure sont conservés. Le jeu peut réattribuer les récompenses de défis déjà terminés. Appliquer ?"),
        ["noUnlockChanges"] = ("No unlock changes needed for this selection.", "Aucun changement de déblocage nécessaire pour cette sélection."),
        ["timeTrialRewards"] = ("Time-attack rewards (records remain unchanged)", "Récompenses de contre-la-montre (chronos conservés)"),
        ["applyGhostProgress"] = ("Apply progress", "Appliquer la progression"),
        ["ghostLevel0"] = ("N. Tropy available", "N. Tropy disponible"),
        ["ghostLevel1"] = ("N. Tropy beaten · Oxide available", "N. Tropy battu · Oxide disponible"),
        ["ghostLevel2"] = ("Oxide beaten · Velo available", "Oxide battu · Velo disponible"),
        ["ghostLevel3"] = ("Velo beaten · Developer available", "Velo battu · Développeur disponible"),
        ["ghostLevel4"] = ("All four challenge ghosts beaten", "Les quatre fantômes battus"),
        ["noGhostProgress"] = ("No saved ghost progress for this track. Complete a time trial in-game first.", "Aucune progression de fantôme enregistrée pour ce circuit. Fais d’abord un contre-la-montre dans le jeu."),
        ["noPlayerGhost"] = ("No player ghost time available.", "Aucun temps de fantôme joueur disponible."),
        ["playerGhostTime"] = ("Recorded player ghost: {0}. Editing records below does not rewrite this replay.", "Fantôme joueur enregistré : {0}. Modifier les records ci-dessous ne réécrit pas ce replay."),
        ["editableTime"] = ("Time (m:ss.cc / seconds)", "Temps (m:ss.cc / secondes)"),
        ["invalidTime"] = ("Invalid time", "Temps invalide"),
        ["The time must preserve the existing ranking order."] = ("The time must preserve the existing ranking order.", "Le temps doit conserver l’ordre du classement existant."),
        ["Enter a finite, positive time."] = ("Enter a finite, positive time.", "Saisis un temps positif et fini."),
        ["Enter a valid time in seconds or m:ss.cc."] = ("Enter a valid time in seconds or m:ss.cc.", "Saisis un temps valide en secondes ou au format m:ss.cc."),
        ["Use seconds, m:ss.cc or m:ss:cc."] = ("Use seconds, m:ss.cc or m:ss:cc.", "Utilise des secondes, m:ss.cc ou m:ss:cc."),
        ["Invalid minutes or seconds."] = ("Invalid minutes or seconds.", "Minutes ou secondes invalides. Les secondes doivent être inférieures à 60."),
        ["Invalid centiseconds."] = ("Invalid centiseconds.", "Centièmes invalides : saisis un nombre entre 0 et 99."),
        ["driverName"] = ("Driver", "Pilote"),
        ["relic"] = ("Relic race", "Course de reliques"),
        ["best_lap"] = ("Best lap", "Meilleur tour"),
        ["trackRewards"] = ("Story progress", "Progression"),
        ["adventureSettings"] = ("Adventure settings", "Réglages Aventure"),
        ["adventureMode"] = ("Adventure", "Aventure"),
        ["advanced"] = ("Advanced", "Avancé"),
        ["gemsTitle"] = ("Gems", "Gemmes"),
        ["oxideTitle"] = ("Nitros Oxide · boss battles", "Nitros Oxide · combats de boss"),
        ["consistencyTitle"] = ("Consistency and dependencies", "Cohérence et dépendances"),
        ["showFlags"] = ("Show technical flags in the progress table", "Afficher les flags techniques dans la progression"),
        ["welcomeTitle"] = ("Your next starting line", "Prépare ta prochaine course"),
        ["welcomeBody"] = ("Choose what you want to edit. Adventure progress and the shared global profile use separate files. Exports keep a backup of the original.", "Choisis ce que tu veux modifier. La progression Aventure et le profil global commun utilisent des fichiers distincts. Les exports conservent un backup de l’original."),
        ["globalProfileHint"] = ("Characters, skins, karts and time attack: use Open global profile (savedata0\\SYS-DATA.sav).", "Personnages, skins, karts et contre-la-montre : utilise Ouvrir le profil global (savedata0\\SYS-DATA.sav)."),
        ["editorSubtitle"] = ("Adventure saves · characters · customization\nCommunity save editor", "Sauvegardes Aventure · personnages · personnalisation\nÉditeur de sauvegardes communautaire"),
        ["unsaved"] = ("Unsaved changes", "Modifications en cours"),
        ["localItems"] = ("Local inventory", "Inventaire local"),
        ["goldenEggs"] = ("Golden Eggs", "Œufs dorés"),
        ["localCoins"] = ("Saved local coins", "Pièces locales sauvegardées"),
        ["progressCaption"] = ("Adventure progress", "Progression Aventure"),
        ["skins"] = ("Skins by driver", "Apparences par pilote"),
        ["skinEvidence"] = ("Choose a driver, then check their skins. Win-based variants share a counter; lowering a threshold can relock other variants.", "Choisis un pilote, puis coche ses apparences. Les variantes gagnées en course partagent un compteur : abaisser un seuil peut en reverrouiller d’autres."),
        ["skinWins"] = ("{0} driver wins", "{0} victoires avec ce pilote"),
        ["skinCascade"] = ("Lowering this shared driver-win counter will also lock the following skins. Apply?", "Abaisser ce compteur de victoires partagé reverrouillera aussi les apparences suivantes. Appliquer ?"),
        ["globalUnlockCascade"] = ("This shared reward condition will also lock the following characters or customization items. Apply?", "Cette condition de récompense partagée reverrouillera aussi les personnages ou personnalisations suivants. Appliquer ?"),
        ["characterWarning"] = ("Choose which drivers are available in your local profile. Skins are managed separately. Server inventory is independent.", "Choisis les pilotes disponibles dans ton profil local. Leurs apparences se règlent séparément. L’inventaire serveur reste indépendant."),
        ["checkAllCharacters"] = ("Unlock all named characters", "Débloquer tous les personnages nommés"),
        ["localPurchase"] = ("local Pit Stop purchase", "achat Pit Stop local"),
        ["baseCharacter"] = ("available by default", "disponible de base"),
        ["conditionCharacter"] = ("profile reward condition", "condition de récompense du profil"),
        ["marketplaceEvidence"] = ("Find a kart, wheel set, paint or sticker. Pit Stop items and known challenge rewards are editable. Shared rewards may also affect characters; server inventory is separate.", "Trouve un kart, des roues, une peinture ou un autocollant. Les achats Pit Stop et récompenses de défis identifiées sont modifiables. Une récompense partagée peut aussi concerner un personnage ; l’inventaire serveur reste séparé."),
        ["CRacerDriverData"] = ("Character", "Personnage"),
        ["CDriverSkinData"] = ("Skin", "Apparence"),
        ["CRacerKartData"] = ("Kart", "Kart"),
        ["CKartCustomizationDecal"] = ("Decal", "Décalcomanie"),
        ["CKartCustomizationSticker"] = ("Sticker", "Autocollant"),
        ["CKartCustomizationColorScheme"] = ("Paint", "Peinture"),
        ["CKartCustomizationItem"] = ("Customization item / wheels", "Accessoire / roues"),
        ["CKartCustomizationPresetItem"] = ("Customization preset", "Ensemble de personnalisation"),
        ["anchoredCharacter"] = (" — confirmed", " — confirmé"),
        ["globalProfile"] = ("Global profile", "Profil global"),
        ["coins"] = ("Saved local Wumpa coins (may differ from server balance)", "Pièces Wumpa locales sauvegardées (peuvent différer du solde serveur)"),
        ["eggEvidence"] = ("Golden Eggs: all five confirmed by in-game captures. Editing eggs still needs an in-game check.", "Œufs dorés : les cinq sont confirmés par des captures en jeu. Modifier les œufs reste à vérifier en jeu."),
        ["eggBit"] = ("Golden Egg — {0}", "Œuf doré — {0}"),
        ["egg1"] = ("N. Sanity Beach", "Plage N. Sanity"),
        ["egg2"] = ("Gem Stone Valley", "Vallée des gemmes"),
        ["egg3"] = ("The Lost Ruins", "Ruines perdues"),
        ["egg4"] = ("Glacier Park", "Parc du Glacier"),
        ["egg5"] = ("Citadel City", "Ville Citadelle"),
        ["eggCount"] = ("Eggs: {0}/5", "Œufs : {0}/5"),
        ["eggsUnavailable"] = ("Egg layout unavailable in this older profile; preserved.", "Disposition des œufs inconnue dans cet ancien profil ; conservée."),
        ["profileItems"] = ("Kart customization", "Personnalisation"),
        ["itemName"] = ("Item / field", "Objet / champ"),
        ["ownership"] = ("Ownership", "Possession"),
        ["evidence"] = ("Evidence", "Preuve"),
        ["unknownOwnership"] = ("Unknown — read-only", "Inconnue — lecture seule"),
        ["observedName"] = ("Name used in a record/counter; does not establish ownership", "Nom utilisé dans un record/compteur ; possession non établie"),
        ["cosmetics"] = ("Stickers, paints, wheels, decals, skins", "Autocollants, peintures, roues, stickers de kart, apparences"),
        ["noItemMap"] = ("No verified item-to-bit mapping in supplied samples", "Aucune correspondance objet/bit vérifiée dans les échantillons"),
        ["unknownMasks"] = ("Advanced", "Avancé"),
        ["value"] = ("Value", "Valeur"),
        ["timeTrials"] = ("Time attack", "Contre-la-montre"),
        ["ttEvidence"] = ("Choose a saved track to adjust ghost challenges or edit an existing record. Ghost recordings and character reward flags are kept separately. New edits need an in-game check.", "Choisis un circuit enregistré pour régler les défis des fantômes ou modifier un record existant. Les replays et récompenses de personnages sont conservés séparément. Ces nouvelles éditions restent à vérifier en jeu."),
        ["category"] = ("Record category", "Catégorie"),
        ["rank"] = ("Rank", "Rang"),
        ["seconds"] = ("Seconds", "Secondes"),
        ["driver"] = ("Driver ID", "Identifiant pilote"),
        ["race"] = ("Race list", "Liste course"),
        ["alternate"] = ("Alternate list (rules unknown)", "Liste alternative (règles inconnues)"),
        ["best_lap_candidate"] = ("Best lap (inferred)", "Meilleur tour (présumé)"),
        ["globalStatus"] = ("Global profile: named unlock conditions and local marketplace catalogue. Export a copy for in-game verification.", "Profil global : conditions de déblocage nommées et catalogue local du Pit Stop. Exporter une copie pour vérification en jeu."),
        ["globalTotals"] = ("Global profile — Wumpa coins: {0} • Golden Eggs: {1}/5", "Profil global — Pièces Wumpa : {0} • Œufs dorés : {1}/5"),
        ["CTR Nitro-Fueled — Éditeur Aventure"] = ("CTR Nitro-Fueled — Adventure editor", "CTR Nitro-Fueled — Éditeur Aventure"),
        ["Vérifier la cohérence"] = ("Check consistency", "Vérifier la cohérence"),
        ["Normaliser"] = ("Normalize", "Normaliser"),
        ["Enregistrer"] = ("Save", "Enregistrer"),
        ["Enregistrer sous…"] = ("Save as…", "Enregistrer sous…"),
        ["Ouvrir un slot…"] = ("Open save (Adventure / Global)…", "Ouvrir une sauvegarde (Aventure / Global)…"),
        ["Gemmes / coupes (ordre des couleurs à confirmer) :"] = ("Gems / gem cups:", "Gemmes / coupes :"),
        ["Aucune correction automatique nécessaire. Les avertissements restent à vérifier."] = ("No automatic corrections needed. Warnings still need review.", "Aucune correction automatique nécessaire. Les avertissements restent à vérifier."),
        ["Appliquer les corrections proposées ?"] = ("Apply proposed corrections?", "Appliquer les corrections proposées ?"),
        ["Zone"] = ("Hub", "Zone"),
        ["Circuit / défi"] = ("Track / challenge", "Circuit / défi"),
        ["Trophée"] = ("Trophy", "Trophée"),
        ["Relique"] = ("Relic", "Relique"),
        ["Boss"] = ("Boss", "Boss"),
        ["Jeton CTR"] = ("CTR Token", "Jeton CTR"),
        ["Couleur"] = ("Colour", "Couleur"),
        ["Autres flags (hex)"] = ("Other flags (hex)", "Autres flags (hex)"),
        ["Aucune"] = ("None", "Aucune"),
        ["Saphir"] = ("Sapphire", "Saphir"),
        ["Or"] = ("Gold", "Or"),
        ["Platine"] = ("Platinum", "Platine"),
        ["Valeur de cellule invalide."] = ("Invalid cell value.", "Valeur de cellule invalide."),
        ["Slot Aventure (*.sav)|*.sav|Tous les fichiers (*.*)|*.*"] = ("Decrypted save (*.sav)|*.sav|All files (*.*)|*.*", "Sauvegarde décryptée (*.sav)|*.sav|Tous les fichiers (*.*)|*.*"),
        ["Slot Aventure (*.sav)|*.sav"] = ("Decrypted save (*.sav)|*.sav", "Sauvegarde décryptée (*.sav)|*.sav"),
        ["Ouvrez un slot Aventure .sav décrypté (153600 octets)."] = ("Open a decrypted Adventure slot or global profile (savedata0).", "Ouvrez un slot Aventure ou un profil global décrypté (savedata0)."),
        ["Boss et dépendances à vérifier en jeu ; progression non décodée conservée. Cliquez sur Vérifier la cohérence."] = ("Boss edits need in-game verification. Unresolved progression preserved. Click Check consistency.", "Boss et dépendances à vérifier en jeu ; progression non décodée conservée. Cliquez sur Vérifier la cohérence."),
        ["Nitros Oxide : bit 18 non décodé, victoire non éditable."] = ("Nitros Oxide: bit 18 unresolved, victory cannot be edited.", "Nitros Oxide : bit 18 non décodé, victoire non éditable."),
        ["Dépendances"] = ("Dependencies", "Dépendances"),
        ["Retirer aussi les récompenses dépendantes ? Refuser annule toute la modification."] = ("Also remove dependent awards? Cancel discards the entire edit.", "Retirer aussi les récompenses dépendantes ? Refuser annule toute la modification."),
        [" Couleur présumée : vérifiez la cohérence et testez une copie en jeu."] = (" Provisional colour: check consistency and test a copy in game.", " Couleur présumée : vérifiez la cohérence et testez une copie en jeu."),
        ["Appliquer"] = ("Apply", "Appliquer"),
        ["Annuler"] = ("Cancel", "Annuler"),
        ["Cohérence Aventure"] = ("Adventure consistency", "Cohérence Aventure"),
        ["Erreur"] = ("Error", "Erreur"),
        ["Avertissement"] = ("Warning", "Avertissement"),
        ["Enregistrer les modifications avant de continuer ?"] = ("Save changes before continuing?", "Enregistrer les modifications avant de continuer ?"),
        ["Modifications non enregistrées"] = ("Unsaved changes", "Modifications non enregistrées"),
        ["Impossible de traiter ce fichier"] = ("Unable to process this file", "Impossible de traiter ce fichier"),
        ["Rouge"] = ("Red", "Rouge"),
        ["Vert"] = ("Green", "Vert"),
        ["Bleu"] = ("Blue", "Bleu"),
        ["Jaune"] = ("Yellow", "Jaune"),
        ["Violet"] = ("Purple", "Violet"),
        ["normalization"] = ("Normalization: {0} fields corrected. Unresolved fields preserved.", "Normalisation : {0} champs corrigés. Champs non décodés conservés."),
        ["oxideUnknown"] = ("Oxide's two victories are separate. Changes add prerequisites or remove dependent victories; advanced doors remain partially decoded.", "Les deux victoires d’Oxide sont distinctes. Les modifications ajoutent les prérequis ou retirent les victoires dépendantes ; portes avancées partiellement décodées."),
        ["difficulty"] = ("Difficulty", "Difficulté"),
        ["easy"] = ("Easy", "Facile"),
        ["medium"] = ("Medium", "Moyenne"),
        ["hard"] = ("Hard", "Difficile"),
        ["oxideFirst"] = ("Oxide: first victory", "Oxide : 1re victoire"),
        ["oxideSecond"] = ("Oxide: rematch victory", "Oxide : revanche gagnée"),
        ["oxideControls"] = ("First / second victory. Edit the two Oxide checkboxes above the table.", "Première / seconde victoire. Utilisez les deux cases Oxide au-dessus du tableau."),
        ["difficultyChanged"] = ("Adventure difficulty updated in header and slot.", "Difficulté Aventure mise à jour dans l’en-tête et le slot."),
        ["oxideTooltip"] = ("Two separate victories: first challenge after 4 keys; final rematch after CTR tokens, gems and relics. Their flags are unresolved. Read-only bit 18: {0}; preserved when removing awards.", "Deux victoires distinctes : premier défi après les 4 clés ; revanche finale après les jetons CTR, gemmes et reliques. Leurs flags ne sont pas décodés. Bit 18 en lecture seule : {0} ; conservé au retrait des récompenses."),
        ["cascade"] = ("Dependencies: trophies {0}→{1}, keys {2}→{3}, tokens {4}→{5}, relics {6}→{7}.", "Dépendances : trophées {0}→{1}, clés {2}→{3}, jetons {4}→{5}, reliques {6}→{7}."),
        ["issueCounts"] = ("{0} error(s), {1} warning(s)", "{0} erreur(s), {1} avertissement(s)"),
        ["awardsTotals"] = ("Trophies: {0}/16     Relics: {1}/18 (sapphire {2}, gold {3}, platinum {4})     Tokens: {5}/20", "Trophées : {0}/16     Reliques : {1}/18 (saphir {2}, or {3}, platine {4})     Jetons : {5}/20"),
        ["colourTotals"] = ("\nRed {0} • Green {1} • Blue {2} • Yellow {3} • Purple {4}    |    Keys: {5}/4 • Gems: {6}/5 • Estimate: {7} %", "\nRouges {0} • Verts {1} • Bleus {2} • Jaunes {3} • Violets {4}    |    Clés : {5}/4 • Gemmes : {6}/5 • Estimation : {7} %"),
        ["Language / Langue"] = ("Language / Langue", "Language / Langue"),
        ["English"] = ("English", "English"),
        ["Français"] = ("Français", "Français"),
        ["true"] = ("Yes", "Oui"),
        ["false"] = ("No", "Non"),
        ["—"] = ("—", "—"),
        ["ioError"] = ("The file could not be read or written. Check its path, access permissions, backups and whether it changed on disk.", "Lecture ou écriture impossible. Vérifiez le chemin, les droits, les sauvegardes de secours et les modifications du fichier sur disque."),
        ["formatError"] = ("Invalid or unsupported save or value. Open a decrypted Adventure slot or a supported global profile.", "Sauvegarde ou valeur invalide ou non prise en charge. Ouvrez un slot Aventure ou un profil global décrypté pris en charge."),
        ["requires"] = ("{0} requires {1}.", "{0} nécessite {1}."),
        ["summary"] = ("{0} does not match awards.", "{0} ne correspond pas aux récompenses."),
        ["slide"] = ("Slide Coliseum requires 10 other relics.", "Stade glissade nécessite 10 autres reliques."),
        ["mystery"] = ("Mystery Caves requires 1 earlier trophy.", "Grotte mystère nécessite 1 trophée précédent."),
        ["relic-tier"] = ("{0}: noncanonical relic bits.", "{0} : bits de relique non canoniques."),
        ["gem-value"] = ("{0} must be 0 or 1.", "{0} doit valoir 0 ou 1."),
        ["gem-cup"] = ("{0}: gem cup flags missing or extra; Normalize repairs them.", "{0} : drapeaux de coupe à gemmes absents ou en trop ; Normaliser les répare."),
        ["unlock-mask"] = ("Captured Ripper Roo unlock bits disagree with their prerequisites.", "Les déblocages capturés de Ripper Roo ne correspondent pas à leurs prérequis."),
        ["mask15"] = ("Progression mask (partially decoded)", "Masque de progression (partiellement décodé)"),
        ["mask16"] = ("Unlock mask (partially decoded)", "Masque de déblocages (partiellement décodé)"),
        ["cursor17"] = ("Event history (word 17, unresolved)", "Historique des événements (mot 17, non résolu)"),
        ["progressIndex"] = ("Progression index", "Indice de progression"),
        ["previousIndex"] = ("Previous level index (travel history)", "Indice du niveau précédent (historique de déplacement)"),
        ["firstHubBit"] = ("First hub captured group, bit {0}", "Groupe capturé du premier hub, bit {0}"),
        ["rooUnlockBit"] = ("Roo reward unlock group, bit {0}", "Groupe de déblocages de Roo, bit {0}"),
        ["unknownProgressBit"] = ("Unresolved progression bit {0}", "Bit de progression non résolu {0}"),
        ["unknownUnlockBit"] = ("Unresolved unlock bit {0}", "Bit de déblocage non résolu {0}"),
        ["farthest"] = ("farthest", "maximal"),
        ["active"] = ("active", "actif"),
        ["rooDoor"] = ("Ripper Roo challenge door", "Porte du défi Ripper Roo"),
        ["unverified"] = ("Ripper Roo is capture-confirmed; other hub bosses remain inferred. Oxide victories use separate slot booleans; track collectible flags are preserved.", "Ripper Roo confirmé par capture ; autres boss de zone déduits. Victoires d’Oxide dans deux booléens distincts ; flags de collectibles des circuits conservés."),
        ["oxide"] = ("Oxide-star collectible bit 18 is present with fewer than 4 keys; preserved independently of the victory booleans.", "Collectible étoile Oxide (bit 18) présent avec moins de 4 clés ; conservé indépendamment des booléens de victoire."),
        ["progression"] = ("Captured early states and Roo unlock group are synchronized. Other bits, advanced indices and word 17 history remain unresolved; complete door/regression safety is unverified.", "États initiaux capturés et groupe de déblocages de Roo synchronisés. Autres bits, indices avancés et historique du mot 17 non résolus ; sécurité complète des portes/régressions non vérifiée."),
        ["gem"] = ("Gem", "Gemme"),
        ["Ripper Roo"] = ("Ripper Roo", "Ripper Roo"),
        ["Papu Papu"] = ("Papu Papu", "Papu Papu"),
        ["Komodo Joe"] = ("Komodo Joe", "Komodo Joe"),
        ["Pinstripe"] = ("Pinstripe", "Pinstripe"),
        ["Nitros Oxide"] = ("Nitros Oxide", "Nitros Oxide"),
        ["windowTitle"] = ("{0}{1} — CTR Nitro-Fueled", "{0}{1} — CTR Nitro-Fueled"),
        ["discard"] = ("Discard", "Ne pas enregistrer"),
        ["close"] = ("Close", "Fermer"),
        ["progress-mask"] = ("Captured mask 15 progression bits disagree with awards.", "Les bits capturés du masque 15 ne correspondent pas aux récompenses."),
        ["progress-header"] = ("Progression indices disagree with the captured early progression state.", "Les indices de progression ne correspondent pas à l’état initial capturé."),
        ["trophies"] = ("Trophies", "Trophées"),
        ["relics"] = ("Relics", "Reliques"),
        ["tokens"] = ("CTR Tokens", "Jetons CTR"),
        ["keys"] = ("Keys", "Clés"),
        ["key"] = ("Key", "Clé"),
        ["gemCup"] = ("Gem cup", "Coupe à gemmes"),
        ["crystal"] = ("Crystal challenge", "Défi de cristaux"),
        ["relicRace"] = ("Relic race", "Course de reliques"),
        ["ctrChallenge"] = ("CTR challenge", "Défi CTR"),
        ["t111_crash_cove"] = ("Crash Cove", "Crique Crash"),
        ["t112_roos_tubes"] = ("Roo's Tubes", "Tubes Roo"),
        ["t113_mystery_caves"] = ("Mystery Caves", "Grotte mystère"),
        ["t114_sewer_speedway"] = ("Sewer Speedway", "Circuit égouts"),
        ["t131_coco_park"] = ("Coco Park", "Parc Coco"),
        ["t132_tiger_temple"] = ("Tiger Temple", "Temple Tigre"),
        ["t133_papus_pyramid"] = ("Papu's Pyramid", "Pyramide Papu"),
        ["t134_dingo_canyon"] = ("Dingo Canyon", "Canyon Dingo"),
        ["t141_blizzard_bluff"] = ("Blizzard Bluff", "Falaises glacées"),
        ["t142_dragon_mines"] = ("Dragon Mines", "Mines dragon"),
        ["t143_polar_pass"] = ("Polar Pass", "Col Polar"),
        ["t144_tiny_arena"] = ("Tiny Arena", "Arène Tiny"),
        ["t151_ngin_labs"] = ("N. Gin Labs", "Labo N. Gin"),
        ["t152_cortex_castle"] = ("Cortex Castle", "Château Cortex"),
        ["t153_hot_air_skyway"] = ("Hot Air Skyway", "Piste air"),
        ["t154_oxide_station"] = ("Oxide Station", "Station Oxide"),
        ["t121_slide_coliseum"] = ("Slide Coliseum", "Stade glissade"),
        ["t122_turbo_track"] = ("Turbo Track", "Circuit turbo"),
        ["a115_skull_rock"] = ("Skull Rock", "Tête de mort"),
        ["a135_rampage_ruins"] = ("Rampage Ruins", "Ruines"),
        ["a145_rocky_road"] = ("Rocky Road", "Chemin ardu"),
        ["a155_nitro_court"] = ("Nitro Court", "Cour nitro"),
        ["N. Sanity Beach"] = ("N. Sanity Beach", "Plage N. Sanity"),
        ["Lost Ruins"] = ("The Lost Ruins", "Ruines perdues"),
        ["Glacier Park"] = ("Glacier Park", "Parc du Glacier"),
        ["Citadel City"] = ("Citadel City", "Ville Citadelle"),
        ["Gem Stone Valley / arenas"] = ("Gem Stone Valley", "Vallée des gemmes"),
    };
    public static string Language { get; set; } = Load();
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ctrnf-save", "settings.json");
    private static string Load()
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            string? language = json.RootElement.GetProperty("language").GetString();
            if (language is "en" or "fr") return language;
        }
        catch (Exception) { /* Settings are optional, including corrupt or inaccessible files. */ }
        return CultureInfo.CurrentUICulture.Name.StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";
    }
    public static void Remember()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { language = Language }));
        }
        catch (Exception) { /* A read-only profile must not prevent editing. */ }
    }
    public static string T(string key) => Language == "fr" ? Strings[key].Fr : Strings[key].En;
    public static string Optional(string key) => Strings.ContainsKey(key) ? T(key) : key;
    public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
    public static string? Key(string text) => Strings.FirstOrDefault(p => (Language == "fr" ? p.Value.Fr : p.Value.En) == text).Key;
    public static string Hub(string key) => T(key);
    public static string Relic(string value) => T(value switch { "none" => "Aucune", "sapphire" => "Saphir", "gold" => "Or", _ => "Platine" });
    public static string Field(string key)
    {
        if (key.StartsWith("track.", StringComparison.Ordinal))
        {
            var parts = key.Split('.');
            return T(parts[1]) + " / " + T(parts[2] switch { "trophy" => "Trophée", "token" => "Jeton CTR", "relic" => "Relique", "boss" => "Boss", _ => "Autres flags (hex)" });
        }
        if (key == "progress.t111_crash_cove.trophy") return T("t111_crash_cove") + " / " + T("progressIndex");
        if (key == "slot.mask_15") return T("mask15");
        if (key == "slot.mask_16") return T("mask16");
        if (key == "slot.unknown_17") return T("cursor17");
        if (key == "header.previous_level_progression_index") return T("previousIndex");
        if (key is "header.farthest_progression_index" or "header.active_progression_index") return T("progressIndex") + " / " + (key.Contains("farthest", StringComparison.Ordinal) ? T("farthest") : T("active"));
        if (key == "unlock.ripper_roo_challenge") return T("rooDoor");
        if (key.StartsWith("progress.first_hub_capture_group_bit", StringComparison.Ordinal)) return F("firstHubBit", key[^2..]);
        if (key.StartsWith("unlock.roo_reward_group_bit", StringComparison.Ordinal)) return F("rooUnlockBit", key[^2..]);
        if (key.StartsWith("progress.unresolved_event_", StringComparison.Ordinal)) return F("unknownProgressBit", key[^2..]);
        if (key.StartsWith("unlock.unresolved_bit_", StringComparison.Ordinal)) return F("unknownUnlockBit", key[^2..]);
        if (key.StartsWith("slot.gem_", StringComparison.Ordinal)) return T("gem") + " / " + Colour(key[9..]);
        string name = key[(key.IndexOf('.') + 1)..];
        if (name is "trophy_count" or "trophy_Count") return T("trophies");
        if (name == "boss_key_count") return T("keys");
        if (name == "relic_count") return T("relics");
        if (name == "ctr_token_count") return T("tokens");
        if (name.StartsWith("relic_", StringComparison.Ordinal)) return T("relics") + " / " + Relic(name[6..]);
        if (name.StartsWith("token_", StringComparison.Ordinal)) return T("tokens") + " / " + Colour(name[6..]);
        return key;
    }
    private static string Colour(string c) => T(c switch { "red" => "Rouge", "green" => "Vert", "blue" => "Bleu", "yellow" => "Jaune", _ => "Violet" });
    public static string Issue(AdventureIssue issue)
    {
        if (issue.Code == "prerequisite") return F("requires", Field(issue.Arguments[0]), Field(issue.Arguments[1]));
        if (issue.Code is "summary" or "gem-value" or "gem-cup") return F(issue.Code, Field(issue.Arguments[0]));
        if (issue.Code == "relic-tier") return F("relic-tier", T(issue.Arguments[0]));
        return T(issue.Code);
    }
    private static string Value(object? value) => value switch
    {
        bool b => T(b ? "true" : "false"),
        string s when s is "none" or "sapphire" or "gold" or "platinum" => Relic(s),
        _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? ""
    };
    public static string Differences(Save before, Save after)
    {
        var a = before.Semantic();
        return string.Join("\r\n", after.Semantic().Where(p => a.TryGetValue(p.Key, out var old) && !JsonOutput.Equal(old, p.Value))
            .Select(p => Field(p.Key) + " : " + Value(a[p.Key]) + " → " + Value(p.Value)));
    }
}
