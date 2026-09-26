import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { ServerError } from '@/components/shared/server-error'
import { TextPrompt } from '@/components/shared/text-prompt'
import { Button } from '@/components/ui/button'
import { useQuestionModeration } from '@/hooks/question'
import type { Question } from '@/services/question/types'

/**
 * What staff can do to one question (specs/076): hide it or show it again, and the same for its answer on its own.
 * Hiding asks why - the author is told - and a refusal (somebody else got there first) is shown in its words.
 */
export function ModerationActions({ question }: { question: Question }) {
  const { t } = useTranslation('admin')
  const moderate = useQuestionModeration(question.id)
  const restored = () => toast.success(t('questions.restored'))
  const hidden = () => toast.success(t('questions.hidden'))

  return (
    <div className="grid gap-2">
      <div className="flex flex-wrap gap-2">
        {question.hiddenAt ? (
          <Button size="sm" variant="outline" className="rounded-full px-3" onClick={() => moderate.restore.mutate(undefined, { onSuccess: restored })}>
            {t('questions.restore')}
          </Button>
        ) : (
          <TextPrompt
            mutation={moderate.hide}
            trigger={t('questions.hide')}
            title={t('questions.hideTitle')}
            description={t('questions.hideBody')}
            label={t('questions.reason')}
            submit={t('questions.confirmHide')}
            onDone={hidden}
            multiline
            maxLength={500}
            destructive
            outline
          />
        )}
        {question.answeredAt !== null &&
          (question.answerHiddenAt ? (
            <Button
              size="sm"
              variant="outline"
              className="rounded-full px-3"
              onClick={() => moderate.restoreAnswer.mutate(undefined, { onSuccess: restored })}
            >
              {t('questions.restoreAnswer')}
            </Button>
          ) : (
            <TextPrompt
              mutation={moderate.hideAnswer}
              trigger={t('questions.hideAnswer')}
              title={t('questions.hideAnswerTitle')}
              description={t('questions.hideAnswerBody')}
              label={t('questions.reason')}
              submit={t('questions.confirmHide')}
              onDone={hidden}
              multiline
              maxLength={500}
              destructive
              outline
            />
          ))}
      </div>
      <ServerError error={moderate.restore.error ?? moderate.restoreAnswer.error} fallback={t('questions.loadFailed')} />
    </div>
  )
}
