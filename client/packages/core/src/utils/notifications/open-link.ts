import { consoleAddress } from '@ecommerce/core/config/apps'

/**
 * Opens a notice's link (specs/137). One into the console - `/admin/...`, as Identity stores them for staff, the stored
 * ones included - opens the back office, another application; everything else is a page of this app.
 */
export function openNoticeLink(link: string, navigate: (to: string) => void) {
  const console = consoleAddress(link)
  if (console) window.location.assign(console)
  else navigate(link)
}
