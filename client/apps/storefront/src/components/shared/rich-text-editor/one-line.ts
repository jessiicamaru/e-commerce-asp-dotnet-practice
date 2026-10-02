/** The paragraphs an editor keeps a line in, gone: a notice is one line, its paragraphs joined by a space. */
export function oneLine(html: string): string {
  return html
    .replace(/<\/p>\s*<p>/g, ' ')
    .replace(/<\/?p>/g, '')
    .trim()
}
