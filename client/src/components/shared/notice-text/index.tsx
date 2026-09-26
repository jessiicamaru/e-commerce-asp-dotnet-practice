import DOMPurify from 'dompurify'
import { cn } from '@/utils/shared'

/** What a notice's words may contain - the server's allow-list (specs/078), held again here. */
const NOTICE_TAGS = ['strong', 'b', 'em', 'i', 'u', 'a']

/**
 * A notice's words, as the HTML `describeNotification` returns: emphasis and links an administrator added, values
 * already escaped. Sanitised again here with the server's allow-list, whatever the server did - this is what runs in
 * the reader's page.
 *
 * <p>
 * `links` is off where the notice itself is a link or a button (the bell, the list): a link inside one is two
 * controls in one, and the notice's own link already goes where it is about. Its words stay, without the anchor.
 * </p>
 */
export function NoticeText({ html, links = false, className }: { html: string; links?: boolean; className?: string }) {
  const clean = DOMPurify.sanitize(html, {
    ALLOWED_TAGS: links ? NOTICE_TAGS : NOTICE_TAGS.filter((tag) => tag !== 'a'),
    ALLOWED_ATTR: ['href'],
    // The web, or a page of the shop - never //elsewhere, which a browser reads as another site.
    ALLOWED_URI_REGEXP: /^(?:https?:\/\/|\/(?!\/))/i,
  })

  return <span className={cn('[&_a]:underline', className)} dangerouslySetInnerHTML={{ __html: clean }} />
}
