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

Le code et la documentation du projet sont sous [PolyForm Noncommercial 1.0.0](../../LICENSE.md). Lecture, modification et partage sont autorisés selon ses termes pour un usage non commercial. L'exploitation commerciale n'est pas accordée : il s'agit de code accessible sous licence non commerciale, pas d'une licence open source OSI.

La police Lilita One possède sa licence SIL OFL séparée. [Crédits et notices](../../THIRD_PARTY_NOTICES.md). L'interface s'inspire visuellement du launcher [OCTR Nitro-Fueled PC](https://github.com/Ryu7w7/OCTR-NitroFueled-PC). Projet communautaire non officiel, sans affiliation aux ayants droit du jeu.
