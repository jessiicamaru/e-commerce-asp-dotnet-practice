import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useAnswerQuestion } from '@/hooks/question'
import type { Question } from '@/services/question/types'

/**
 * Answers a question, or rewrites the answer (specs/076). Folded to one button until opened, so a queue of twenty
 * questions is not twenty text boxes. Whether the caller may answer is the server's decision - a refusal is shown
 * in its words: a hidden answer, for one, is locked until staff restore it.
 */
export function AnswerForm({ question }: { question: Question }) {
  const { t } = useTranslation('catalog')
  const id = useId()
  const answer = useAnswerQuestion(question.id)
  const [open, setOpen] = useState(false)
  const [text, setText] = useState(question.answer ?? '')
  const rewriting = question.answeredAt !== null

  if (!open) {
    return (
      <Button type="button" variant="outline" size="sm" className="justify-self-start rounded-full px-3" onClick={() => setOpen(true)}>
        {rewriting ? t('questions.editAnswer') : t('questions.answer')}
      </Button>
    )
  }

  const submit = (event: FormEvent) => {
    event.preventDefault()
    answer.mutate(text.trim(), {
      onSuccess: () => {
        setOpen(false)
        toast.success(t('questions.answered'))
      },
    })
  }

  return (
    <form onSubmit={submit} className="grid gap-2">
      <Label htmlFor={id}>{t('questions.yourAnswer')}</Label>
      <Textarea id={id} autoFocus maxLength={2000} value={text} onChange={(event) => setText(event.target.value)} />
      <ServerError error={answer.error} fallback={t('questions.failed')} />
      <div className="flex gap-2">
        <Button type="submit" size="sm" className="rounded-full px-4" disabled={!text.trim() || answer.isPending}>
          {t('questions.sendAnswer')}
        </Button>
        <Button type="button" size="sm" variant="ghost" className="rounded-full px-3" onClick={() => setOpen(false)}>
          {t('action.cancel', { ns: 'common' })}
        </Button>
      </div>
    </form>
  )
}
