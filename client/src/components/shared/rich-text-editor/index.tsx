import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { EditorContent, useEditor, type Editor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import {
  BoldIcon,
  Heading2Icon,
  ItalicIcon,
  LinkIcon,
  ListIcon,
  ListOrderedIcon,
  QuoteIcon,
  Redo2Icon,
  StrikethroughIcon,
  UnderlineIcon,
  Undo2Icon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/utils/shared'
import { oneLine } from './one-line'

/** A placeholder standing for an address, like `{link}` - what a link may point at besides a real one. */
const PLACEHOLDER = /^\{[A-Za-z]+\}$/

/**
 * Rich text for words the shop sends (specs/077): TipTap, limited to what the server's allow-list keeps - bold,
 * italic, underline, strike, a heading, lists, a quote and links - so what the administrator sees is what is sent.
 * Placeholders are inserted from `placeholders` at the cursor rather than typed, and a link may point at one.
 *
 * <p>
 * The editor holds its own content once mounted: give it a `key` that changes when the words should be replaced
 * (another template, a reset, a restore) rather than expecting `value` to be pushed in.
 * </p>
 * <p>
 * `variant="inline"` is a notice (specs/078): one line - bold, italic, underline and links, no blocks - and what
 * `onChange` gets is that line, without the paragraph the editor keeps it in. `token` writes a placeholder the way
 * its words spell one: `{name}` in an email, `{{name}}` in a notice.
 * </p>
 */
export function RichTextEditor({
  id,
  value,
  onChange,
  placeholders,
  variant = 'full',
  token = (name) => `{${name}}`,
}: {
  id: string
  value: string
  onChange: (html: string) => void
  placeholders: string[]
  variant?: 'full' | 'inline'
  token?: (name: string) => string
}) {
  const { t } = useTranslation('common')
  const inline = variant === 'inline'
  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        code: false,
        codeBlock: false,
        heading: inline ? false : { levels: [2, 3] },
        ...(inline
          ? { bulletList: false, orderedList: false, listItem: false, blockquote: false, horizontalRule: false, strike: false, hardBreak: false }
          : {}),
        link: {
          openOnClick: false,
          autolink: false,
          HTMLAttributes: { rel: null, target: null },
          isAllowedUri: (url, ctx) => PLACEHOLDER.test(url) || ctx.defaultValidate(url),
        },
      }),
    ],
    content: value,
    shouldRerenderOnTransaction: true,
    editorProps: { attributes: { id, class: cn('px-4 py-3 outline-none', inline ? 'min-h-12' : 'min-h-48') } },
    onUpdate: ({ editor: current }) => onChange(inline ? oneLine(current.getHTML()) : current.getHTML()),
  })

  if (!editor) return null

  return (
    <div className="ring-border/60 bg-card rounded-2xl ring-1">
      <Toolbar editor={editor} inline={inline} />
      <div className="email-body text-sm leading-relaxed">
        <EditorContent editor={editor} />
      </div>
      <div className="border-border/60 flex flex-wrap items-center gap-1.5 border-t px-3 py-2">
        <span className="text-muted-foreground text-xs">{t('editor.insert')}</span>
        {placeholders.map((name) => (
          <Button
            key={name}
            type="button"
            size="sm"
            variant="outline"
            className="h-7 rounded-full px-2.5 font-mono text-xs"
            onClick={() => editor.chain().focus().insertContent(token(name)).run()}
          >
            {token(name)}
          </Button>
        ))}
      </div>
    </div>
  )
}

function Toolbar({ editor, inline }: { editor: Editor; inline: boolean }) {
  const { t } = useTranslation('common')
  const [linking, setLinking] = useState(false)
  const [href, setHref] = useState('')

  const mark = (label: string, icon: ReactNode, active: boolean, run: () => void) => (
    <Button
      type="button"
      size="icon"
      variant="ghost"
      aria-label={label}
      title={label}
      aria-pressed={active}
      className={cn('size-8 rounded-lg', active && 'bg-muted')}
      onClick={run}
    >
      {icon}
    </Button>
  )

  return (
    <div className="border-border/60 grid gap-2 border-b p-2">
      <div className="flex flex-wrap gap-0.5">
        {mark(t('editor.bold'), <BoldIcon />, editor.isActive('bold'), () => editor.chain().focus().toggleBold().run())}
        {mark(t('editor.italic'), <ItalicIcon />, editor.isActive('italic'), () => editor.chain().focus().toggleItalic().run())}
        {mark(t('editor.underline'), <UnderlineIcon />, editor.isActive('underline'), () => editor.chain().focus().toggleUnderline().run())}
        {!inline && (
          <>
            {mark(t('editor.strike'), <StrikethroughIcon />, editor.isActive('strike'), () => editor.chain().focus().toggleStrike().run())}
            {mark(t('editor.heading'), <Heading2Icon />, editor.isActive('heading', { level: 2 }), () =>
              editor.chain().focus().toggleHeading({ level: 2 }).run(),
            )}
            {mark(t('editor.bulletList'), <ListIcon />, editor.isActive('bulletList'), () => editor.chain().focus().toggleBulletList().run())}
            {mark(t('editor.orderedList'), <ListOrderedIcon />, editor.isActive('orderedList'), () =>
              editor.chain().focus().toggleOrderedList().run(),
            )}
            {mark(t('editor.quote'), <QuoteIcon />, editor.isActive('blockquote'), () => editor.chain().focus().toggleBlockquote().run())}
          </>
        )}
        {mark(t('editor.link'), <LinkIcon />, editor.isActive('link'), () => {
          setHref((editor.getAttributes('link').href as string | undefined) ?? '')
          setLinking((open) => !open)
        })}
        {mark(t('editor.undo'), <Undo2Icon />, false, () => editor.chain().focus().undo().run())}
        {mark(t('editor.redo'), <Redo2Icon />, false, () => editor.chain().focus().redo().run())}
      </div>
      {linking && (
        <div className="flex flex-wrap gap-2">
          <Input
            aria-label={t('editor.linkAddress')}
            placeholder="https://… / {link}"
            className="h-8 max-w-xs rounded-lg font-mono text-xs"
            value={href}
            onChange={(event) => setHref(event.target.value)}
          />
          <Button
            type="button"
            size="sm"
            className="h-8 rounded-full px-3"
            disabled={!href.trim()}
            onClick={() => {
              editor.chain().focus().extendMarkRange('link').setLink({ href: href.trim() }).run()
              setLinking(false)
            }}
          >
            {t('editor.applyLink')}
          </Button>
          <Button
            type="button"
            size="sm"
            variant="ghost"
            className="h-8 rounded-full px-3"
            onClick={() => {
              editor.chain().focus().extendMarkRange('link').unsetLink().run()
              setLinking(false)
            }}
          >
            {t('editor.removeLink')}
          </Button>
        </div>
      )}
    </div>
  )
}
