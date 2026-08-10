# lectarr

**lectarr** est un gestionnaire d'ebooks et de livres audio (Usenet + BitTorrent)
avec une **vraie gestion des langues** — pensé pour récupérer les livres en
français (ou dans n'importe quelle langue) au lieu de tout rapatrier en anglais.

C'est un fork de [bookshelf](https://github.com/pennydreadful/bookshelf)
(lui-même la continuation de [Readarr](https://github.com/Readarr/Readarr),
abandonné), accompagné d'un proxy de métadonnées
[rreading-glasses](https://github.com/blampe/rreading-glasses) patché pour que
les éditions traduites existent enfin côté métadonnées.

*lectarr is a fork of bookshelf (the Readarr revival) adding first-class
release/edition language support — parse, require, prefer and search books in
your language. The README is in French; the code, settings and commit
messages are in English.*

## Ce que lectarr ajoute

- **Parsing de langue des releases** : les tags `FRENCH`, `TRUEFRENCH`, `VF`,
  `FRA`, `MULTI`, `[FR]`… sont détectés dans les noms de release (une
  vingtaine de langues supportées). Les codes courts (`FR`, `EN`, `DE`…) ne
  matchent qu'en majuscules pour éviter les faux positifs de noms de domaine.
- **Condition de format personnalisé « Language »** : score (préférer) ou
  exige (via `Minimum Custom Format Score`) une langue dans les profils de
  qualité.
- **Langue exigible dans le profil de qualité** : profil réglé sur `French` →
  toute release non française (ou de langue inconnue) est rejetée.
- **« Default Release Language » par indexer** (Newznab/Torznab, réglage
  avancé) : pour les trackers 100 % francophones qui ne taguent pas leurs
  releases — toutes leurs releases non taguées comptent comme françaises.
- **Langue d'édition préférée** (profil de métadonnées, code ISO 639-3, ex.
  `fra`) : quand un livre a une édition dans cette langue, elle devient
  l'édition de référence — son titre sert à l'affichage **et à la recherche**.
  Elle n'est jamais purgée par le filtre `AllowedLanguages`.
- **Recherche multi-titres** : chaque recherche interroge aussi les indexers
  avec les titres des autres éditions (plafonné) — une VF nommée « Le Problème
  à trois corps » est trouvée même si le livre est référencé « The Three-Body
  Problem ». Le matching reconnaît les releases nommées d'après n'importe
  quelle édition, et un tier de recherche ISBN est utilisé quand l'indexer le
  supporte.
- **Métadonnées** : le proxy embarqué pagine les éditions Goodreads (~20 →
  100) et ré-attribue les éditions traduites créditées au traducteur à leur
  auteur — les deux raisons principales pour lesquelles les éditions FR
  n'apparaissaient jamais. (Patch également proposé upstream.)

## Démarrage rapide

```bash
git clone https://github.com/greenlynxe/lectarr.git
cd lectarr
# adapter les volumes /books et /downloads dans docker-compose.yml
docker compose up -d --build
```

Le premier build compile le backend .NET, le frontend et le proxy Go — compte
plusieurs minutes. L'interface est ensuite sur `http://<hôte>:8787`, le proxy
de métadonnées sur le port 8788.

Base de données Readarr/bookshelf (softcover) existante : compatible — pointez
simplement l'instance sur ce déploiement. Les migrations (langue des profils)
s'appliquent automatiquement au premier démarrage.

## Configuration recommandée (français)

1. **Settings → Profiles → Metadata Profile** :
   `Preferred Edition Language` = `fra`, et `AllowedLanguages` =
   `eng, fre, fra, null`.
2. **Settings → Profiles → Quality Profile** : `Language` = `French` pour
   *exiger* le français, ou laisser `Any` et préférer via un format
   personnalisé.
3. **Settings → Custom Formats** : créer un format « VF » avec une condition
   `Language = French`, puis lui donner un score élevé dans le profil de
   qualité (`Minimum Custom Format Score > 0` pour l'exiger).
4. **Indexers** : sur chaque tracker francophone (via Prowlarr/Torznab),
   régler `Default Release Language` = `French` (réglage avancé).
5. **MyAnonaMouse** : utiliser en plus son filtre natif `Search Languages`.
6. Relancer un refresh des auteurs (`System → Tasks`) pour récupérer les
   éditions françaises, puis vérifier sur un livre que l'édition FR est bien
   monitorée (Edit Book → Edition).

## Architecture

| Service | Rôle | Port |
|---|---|---|
| `lectarr` | l'application (fork bookshelf) | 8787 |
| `rreading-glasses` | proxy de métadonnées Goodreads patché | 8788 |
| `metadata-db` | PostgreSQL du proxy | — |

`METADATA_URL` pointe par défaut sur le proxy local ; il est possible
d'utiliser l'instance publique `https://api.bookinfo.pro` à la place (sans les
correctifs d'éditions traduites tant qu'ils ne sont pas mergés upstream).

## Crédits & licence

lectarr n'existe que grâce au travail de
[bookshelf](https://github.com/pennydreadful/bookshelf),
[rreading-glasses](https://github.com/blampe/rreading-glasses),
[Readarr](https://github.com/Readarr/Readarr) et de tout l'écosystème
\*arr / Servarr. Les modifications ont vocation à être proposées upstream.

Distribué sous licence GPLv3, comme Readarr et bookshelf — voir
[LICENSE.md](LICENSE.md).
