import type { Choice } from '@/utils/shared'

/** Every ISO 3166-1 alpha-2 code. The server validates the code; this only saves typing it. */
const CODES = `AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS
BT BV BW BY BZ CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES
ET FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU ID IE IL IM
IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME
MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG
PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY
SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM
ZW`.split(/\s+/)

/** Where most of this shop's customers are, so it is at the top rather than under "Vanuatu". */
const FIRST = 'VN'

/**
 * Countries as choices, named in `language` - "Việt Nam" or "Vietnam" - with the code as the hint,
 * so typing either "viet" or "VN" finds it.
 *
 * The names come from the browser (`Intl.DisplayNames`), which already knows them in every language;
 * a table of 249 names in two languages kept here would be a thing to get wrong.
 */
export function countryChoices(language: string): Choice[] {
  let names: Intl.DisplayNames | null = null
  try {
    names = new Intl.DisplayNames([language], { type: 'region' })
  } catch {
    // An engine without DisplayNames still gets a usable list: the codes themselves.
  }

  const choices = CODES.map((code) => ({ value: code, label: names?.of(code) ?? code, hint: code }))
  const first = choices.filter((choice) => choice.value === FIRST)
  const rest = choices
    .filter((choice) => choice.value !== FIRST)
    .sort((a, b) => a.label.localeCompare(b.label, language))

  return [...first, ...rest]
}
