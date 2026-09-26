# Where the product photographs came from

The image files themselves are **not in this repository** — `server/seed/images/` is gitignored,
and they live on the local `catalog_images` Docker volume. This file is the record of what was
used and under what terms, because CC BY and CC BY-SA **require attribution** and an attribution
that exists only in somebody's shell history is not one.

Fetched by hand from the pages below and uploaded with `seed/seed-images.py`, which fetches nothing itself; see [seed/README.md](README.md).

## Wikimedia Commons

Almost all of them are Henry Söderlund's studio photographs, which is why the catalogue reads as
one shop rather than as thirteen different rooms. Reusing these means keeping the credit and, for
CC BY-SA, licensing any modified version the same way.

| SKU | File | Photographer | Licence |
| :-- | :-- | :-- | :-- |
| `CANON-R50` | [Canon EOS R50 (52694437103).jpg](https://commons.wikimedia.org/wiki/File:Canon_EOS_R50_(52694437103).jpg) | Henry Söderlund from Helsinki, Finland | CC BY 2.0 |
| `CANON-R6M2` | [Canon EOS R6 Mark II - by Henry Söderlund (52546794891).jpg](https://commons.wikimedia.org/wiki/File:Canon_EOS_R6_Mark_II_-_by_Henry_S%C3%B6derlund_(52546794891).jpg) | Henry Söderlund from Helsinki, Finland | CC BY 2.0 |
| `CANON-R8` | [Canon EOS R8 (52853735946).jpg](https://commons.wikimedia.org/wiki/File:Canon_EOS_R8_(52853735946).jpg) | Henry Söderlund from Helsinki, Finland | CC BY 2.0 |
| `FUJI-XS20` | [Fujifilm X-S20 by Henry Söderlund.jpg](https://commons.wikimedia.org/wiki/File:Fujifilm_X-S20_by_Henry_S%C3%B6derlund.jpg) | Henry Söderlund | CC BY 2.0 |
| `FUJI-XT5` | [Fujifilm X-T5 with Fujinon XF 35mm F2 R WR - by Henry Söderlund (52536299126).jpg](https://commons.wikimedia.org/wiki/File:Fujifilm_X-T5_with_Fujinon_XF_35mm_F2_R_WR_-_by_Henry_S%C3%B6derlund_(52536299126).jpg) | Henry Söderlund from Helsinki, Finland | CC BY 2.0 |
| `NIKON-Z6III` | [Nikon Z6III (by Henry Söderlund).jpg](https://commons.wikimedia.org/wiki/File:Nikon_Z6III_(by_Henry_S%C3%B6derlund).jpg) | Henry Söderlund | CC BY 2.0 |
| `NIKON-ZF` | [Nikon Z f with Nikkor Z 26mm F2.8 - by Henry Söderlund (53361659701).jpg](https://commons.wikimedia.org/wiki/File:Nikon_Z_f_with_Nikkor_Z_26mm_F2.8_-_by_Henry_S%C3%B6derlund_(53361659701).jpg) | Henry Söderlund from Helsinki, Finland | CC BY 2.0 |
| `OM-OM5` | [OM System OM-5 (52452521396).jpg](https://commons.wikimedia.org/wiki/File:OM_System_OM-5_(52452521396).jpg) | Henry Söderlund from Helsinki, Finland | CC BY 2.0 |
| `PANA-S5M2` | [Panasonic LUMIX S5 II (52682131682).jpg](https://commons.wikimedia.org/wiki/File:Panasonic_LUMIX_S5_II_(52682131682).jpg) | Henry Söderlund from Helsinki, Finland | CC BY 2.0 |
| `RICOH-GR3X` | [Ricoh GR IIIx – by Henry Söderlund (51695015979).jpg](https://commons.wikimedia.org/wiki/File:Ricoh_GR_IIIx_%E2%80%93_by_Henry_S%C3%B6derlund_(51695015979).jpg) | Henry Söderlund from Helsinki, Finland, Finland | CC BY 2.0 |
| `SONY-A7CII` | [Sony A7C II by Henry Söderlund.jpg](https://commons.wikimedia.org/wiki/File:Sony_A7C_II_by_Henry_S%C3%B6derlund.jpg) | Henry Söderlund | CC BY 2.0 |
| `SONY-A7M4` | [Sony A7 IV (ILCE-7M4) - by Henry Söderlund (51739988735).jpg](https://commons.wikimedia.org/wiki/File:Sony_A7_IV_(ILCE-7M4)_-_by_Henry_S%C3%B6derlund_(51739988735).jpg) | Henry Söderlund from Helsinki, Finland, Finland | CC BY 2.0 |
| `SONY-ZVE10M2` | [Sony ZV-E10 Mark II.tiff](https://commons.wikimedia.org/wiki/File:Sony_ZV-E10_Mark_II.tiff) | PJ | CC BY 4.0 |

## Not on Commons

| SKU | Product | What is there instead |
| :-- | :-- | :-- |
| `FUJI-X100VI` | Fujifilm X100VI | A promotional image from a retailer's product page. **Not licensed for reuse** - it is a local placeholder and must not be committed or published. Commons has only the 2011 X100, which is a different camera, and shipping that under this name would be quietly wrong. |

## If you are re-running this

⚠️ **A file name is not evidence.** Searching Commons for the Panasonic returned `Harbor rope.jpg`
and searching for the X100VI returned the 2011 X100. Every file here was downloaded and then
**looked at** in a contact sheet before being used. Do the same.

⚠️ **The right model, not the closest one.** Commons has a Söderlund studio shot of the Sony
ZV-E10 but not of the ZV-E10 **II**; the II keeps a plainer photograph by a different author
instead, because showing the previous generation under this product's name is worse than a set
that does not quite match.
