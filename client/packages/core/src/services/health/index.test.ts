/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import en from '@ecommerce/core/locales/en/status.json'
import vi from '@ecommerce/core/locales/vi/status.json'
import { HEALTH_SERVICES } from '.'

/** The gateway's health routes - `/api/<service>/health` - read from its own configuration. */
function gatewayHealthServices(): string[] {
  const path = resolve(import.meta.dirname, '../../../../../../server/src/ApiGateway/Ecommerce.ApiGateway/appsettings.json')
  const config = JSON.parse(readFileSync(path, 'utf8').replace(/^﻿/, ''))
  const routes = Object.values(config.ReverseProxy.Routes) as { Match: { Path: string } }[]
  return routes.map((route) => /^\/api\/([a-z]+)\/health$/.exec(route.Match.Path)?.[1]).filter((name): name is string => !!name)
}

describe('the status page lists every service (specs/121, #244)', () => {
  it('asks every service the gateway has a health route for, and nothing else', () => {
    expect([...HEALTH_SERVICES].sort()).toEqual(gatewayHealthServices().sort())
  })

  it.each([
    ['en', en.service],
    ['vi', vi.service],
  ])('names each one in %s', (_, names) => {
    expect(Object.keys(names).sort()).toEqual([...HEALTH_SERVICES].sort())
  })
})
