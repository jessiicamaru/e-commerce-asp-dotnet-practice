// Shoppers browse the catalogue (specs/144, US3): anonymous reads through the gateway - a page of products, a search,
// one product - ramping up and holding. No sign-in and no writes, so nothing to clean up.
import http from 'k6/http'
import { check, sleep } from 'k6'
import { GATEWAY, summary, reporting } from './lib/shop.js'

const PEAK = Number(__ENV.PEAK || 50)
const searches = ['camera', 'fuji', 'sony', 'lens', 'máy ảnh', 'canon']

export const options = {
  scenarios: {
    browse: {
      executor: 'ramping-vus',
      stages: [
        { duration: '30s', target: PEAK },
        { duration: '60s', target: PEAK },
        { duration: '10s', target: 0 },
      ],
    },
  },
  ...reporting(['list products', 'search', 'one product'], { http_req_failed: ['rate==0'], checks: ['rate==1'] }),
}

export function setup() {
  const page = http.get(`${GATEWAY}/api/products?page=1&pageSize=12`).json()
  const ids = (page.items || []).map((p) => p.id)
  if (ids.length === 0) throw new Error('the catalogue is empty - seed it first: python seed/seed-catalogue.py')
  return { ids }
}

export default function (data) {
  const page = 1 + Math.floor(Math.random() * 3)
  const list = http.get(`${GATEWAY}/api/products?page=${page}&pageSize=12`, { tags: { name: 'list products' } })
  check(list, { 'list is 200': (r) => r.status === 200 })

  const term = encodeURIComponent(searches[Math.floor(Math.random() * searches.length)])
  const search = http.get(`${GATEWAY}/api/products?search=${term}&page=1&pageSize=12`, { tags: { name: 'search' } })
  check(search, { 'search is 200': (r) => r.status === 200 })

  const id = data.ids[Math.floor(Math.random() * data.ids.length)]
  const one = http.get(`${GATEWAY}/api/products/${id}`, { tags: { name: 'one product' } })
  check(one, { 'product is 200': (r) => r.status === 200 })

  sleep(1)   // a person reads the page
}

export const handleSummary = summary('browse', { peak_vus: PEAK, stages: '30s up, 60s hold, 10s down', think_time: '1s' })
