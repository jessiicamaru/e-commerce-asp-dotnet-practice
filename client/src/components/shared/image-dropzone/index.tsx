import { useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ImageUpIcon, LoaderCircleIcon } from 'lucide-react'
import { cn } from '@/utils/shared'
import { IMAGE_TYPES, imageProblem } from './image-problem'

/**
 * Somewhere to drop a photograph, or to click and choose one.
 *
 * <p>
 * The current picture is shown inside the drop area, so replacing it is dropping onto it - there is
 * no second place to look. The whole area is a real button (keyboard, screen reader), with the file
 * input hidden behind it rather than removed.
 * </p>
 * <p>
 * shadcn has no dropzone, so this is one of the few components here that is not generated. It is
 * built from nothing but a button and an input, and it stays small on purpose.
 * </p>
 */
export function ImageDropzone({
  onFile,
  busy = false,
  preview,
  compact = false,
  label,
  className,
}: {
  onFile: (file: File) => void
  busy?: boolean
  /** What is there now - drawn inside the area, under the prompt. */
  preview?: React.ReactNode
  /** A small square, for one variant's picture beside its prices. */
  compact?: boolean
  /** The accessible name, e.g. "Photograph of Kit: Body only". */
  label: string
  className?: string
}) {
  const { t } = useTranslation()
  const input = useRef<HTMLInputElement>(null)
  const id = useId()
  const [over, setOver] = useState(false)
  const [problem, setProblem] = useState<'type' | 'size' | null>(null)

  function take(file: File | undefined) {
    if (!file || busy) return
    const refused = imageProblem(file)
    setProblem(refused)
    if (!refused) onFile(file)
  }

  return (
    <div className={cn('grid gap-1.5', className)}>
      <button
        type="button"
        aria-label={label}
        aria-describedby={`${id}-hint`}
        disabled={busy}
        onClick={() => input.current?.click()}
        onDragOver={(event) => {
          event.preventDefault()
          setOver(true)
        }}
        onDragLeave={() => setOver(false)}
        onDrop={(event) => {
          event.preventDefault()
          setOver(false)
          take(event.dataTransfer.files?.[0])
        }}
        className={cn(
          'group relative grid w-full place-items-center overflow-hidden border-2 border-dashed transition-colors outline-none',
          'focus-visible:ring-ring/50 focus-visible:ring-3 disabled:cursor-wait',
          compact ? 'aspect-square rounded-2xl' : 'aspect-square rounded-3xl',
          over ? 'border-primary bg-accent' : 'border-border hover:border-primary/60',
        )}
      >
        {preview && <span className="absolute inset-0 grid place-items-center [&>*]:size-full">{preview}</span>}

        <span
          className={cn(
            'relative z-10 grid place-items-center gap-1.5 rounded-2xl px-3 py-2 text-center transition-opacity',
            preview && 'bg-card/90 opacity-0 shadow-sm group-hover:opacity-100 group-focus-visible:opacity-100',
            (over || busy) && 'opacity-100',
          )}
        >
          {busy ? (
            <LoaderCircleIcon className="text-muted-foreground size-6 animate-spin" />
          ) : (
            <ImageUpIcon className={cn('text-muted-foreground', compact ? 'size-5' : 'size-7')} />
          )}
          {!compact && (
            <>
              <span className="text-sm font-semibold">{busy ? t('dropzone.uploading') : t('dropzone.prompt')}</span>
              <span className="text-muted-foreground text-xs">{t('dropzone.or')}</span>
            </>
          )}
        </span>
      </button>

      <input
        ref={input}
        type="file"
        accept={IMAGE_TYPES.join(',')}
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        onChange={(event) => {
          take(event.target.files?.[0])
          event.target.value = '' // so choosing the same file again still fires
        }}
      />

      <p id={`${id}-hint`} className={cn('text-xs', problem ? 'text-destructive' : 'text-muted-foreground')}>
        {problem === 'type' ? t('dropzone.badType') : problem === 'size' ? t('dropzone.tooBig') : !compact && t('dropzone.hint')}
      </p>
    </div>
  )
}
