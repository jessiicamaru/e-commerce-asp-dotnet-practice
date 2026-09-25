import { useId, useState, type ReactNode } from 'react'
import type { UseMutationResult } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

/**
 * A button that opens a dialog asking for one piece of text - a reason, a tracking reference - and sends it
 * through the mutation it is handed (specs/067). Empty text cannot be sent, the dialog closes only once the
 * server accepted it, and a refusal is shown in the server's words inside the dialog, where it was caused.
 */
export function TextPrompt({
  mutation,
  trigger,
  title,
  description,
  label,
  submit,
  onDone,
  multiline = false,
  maxLength,
  placeholder,
  destructive = false,
  outline = false,
}: {
  mutation: UseMutationResult<unknown, Error, string>
  /** What the button says, icon included. */
  trigger: ReactNode
  title: string
  description: string
  label: string
  submit: string
  /** After the server accepted it - a toast, usually. */
  onDone?: () => void
  multiline?: boolean
  maxLength: number
  placeholder?: string
  destructive?: boolean
  /** A quieter trigger, for the second of two choices. */
  outline?: boolean
}) {
  const { t } = useTranslation('common')
  const id = useId()
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const Field = multiline ? Textarea : Input

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) mutation.reset()
      }}
    >
      <DialogTrigger
        render={
          <Button
            variant={outline ? 'outline' : 'default'}
            className="h-9 justify-self-start rounded-full px-4"
            disabled={mutation.isPending}
          />
        }
      >
        {trigger}
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <form
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            mutation.mutate(text.trim(), {
              onSuccess: () => {
                setOpen(false)
                setText('')
                onDone?.()
              },
            })
          }}
        >
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-2">
            <Label htmlFor={id}>{label}</Label>
            <Field
              id={id}
              autoFocus
              required
              maxLength={maxLength}
              placeholder={placeholder}
              className={multiline ? undefined : 'h-10 rounded-xl font-mono'}
              value={text}
              onChange={(event: { target: { value: string } }) => setText(event.target.value)}
            />
          </div>
          <ServerError error={mutation.error} fallback={t('error.generic')} />
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="ghost" />}>{t('action.cancel')}</DialogClose>
            <Button type="submit" variant={destructive ? 'destructive' : 'default'} disabled={mutation.isPending || !text.trim()}>
              {submit}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
