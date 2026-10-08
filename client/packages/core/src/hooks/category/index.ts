import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Category } from '@ecommerce/core/services/category'
import type { NewSpecification } from '@ecommerce/core/services/category/types'

/** Every category as it reads in one language - for the administrators' page, which shows two side by side. */
export function useCategoriesIn(language: string) {
  return useQuery({ queryKey: queryKeys.categoriesIn(language), queryFn: () => Category.listIn(language) })
}

/** Create, move (specs/158), rename, translate and delete (specs/097); each refreshes every list of categories. */
export function useCategoryChanges() {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: queryKeys.categories() })
  return {
    create: useMutation({
      mutationFn: (input: { name: string; slug: string; description: string | null; parentCategoryId?: string | null }) =>
        Category.create(input.name, input.slug, input.description, input.parentCategoryId ?? null),
      onSuccess: refresh,
    }),
    move: useMutation({
      mutationFn: (input: { id: string; parentCategoryId: string | null }) => Category.move(input.id, input.parentCategoryId),
      onSuccess: refresh,
    }),
    save: useMutation({
      mutationFn: async (input: { id: string; vi: Text; en: Text }) => {
        await Category.update(input.id, input.vi.name, input.vi.description)
        if (input.en.name.trim()) await Category.translate(input.id, 'en', input.en.name, input.en.description)
        else await Category.removeTranslation(input.id, 'en')
      },
      onSuccess: refresh,
    }),
    remove: useMutation({ mutationFn: (id: string) => Category.remove(id), onSuccess: refresh }),
  }
}

type Text = { name: string; description: string | null }

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories(),
    queryFn: () => Category.list(),
    // Categories change rarely; this keeps the filter from refetching on every visit.
    staleTime: 5 * 60_000,
  })
}

/** What applies to products filed under a category (specs/159); nothing is asked without one. */
export function useCategorySpecifications(categoryId: string | null | undefined) {
  return useQuery({
    queryKey: queryKeys.categorySpecifications(categoryId ?? ''),
    queryFn: () => Category.specifications(categoryId!),
    enabled: !!categoryId,
    staleTime: 5 * 60_000,
  })
}

/** A category's specifications in one language - the administrators' page reads two side by side. */
export function useCategorySpecificationsIn(categoryId: string, language: string) {
  return useQuery({
    queryKey: [...queryKeys.categorySpecifications(categoryId), language],
    queryFn: () => Category.specificationsIn(categoryId, language),
  })
}

/** Declaring specifications (specs/159); each refreshes every list of categories and specifications. */
export function useSpecificationChanges(categoryId: string) {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: queryKeys.categories() })
  return {
    create: useMutation({
      mutationFn: async ({ english, optionsEnglish, ...specification }: NewSpecification & { english: string; optionsEnglish?: string[] }) => {
        // Only what the server declares: the English goes in its own requests, after.
        const created = await Category.createSpecification(categoryId, specification)
        if (english.trim()) await Category.translateSpecification(categoryId, created.id, 'en', english.trim())
        for (const [index, option] of created.options.entries()) {
          const optionEnglish = optionsEnglish?.[index]?.trim()
          if (optionEnglish) await Category.translateOption(categoryId, created.id, option.id, 'en', optionEnglish)
        }
        return created
      },
      onSuccess: refresh,
    }),
    remove: useMutation({ mutationFn: (id: string) => Category.removeSpecification(categoryId, id), onSuccess: refresh }),
    addOption: useMutation({
      mutationFn: async (input: { specificationId: string; code: string; value: string; english: string }) => {
        const updated = await Category.addOption(categoryId, input.specificationId, input.code, input.value)
        const option = updated.options.find((o) => o.code === input.code)
        if (option && input.english.trim()) {
          await Category.translateOption(categoryId, input.specificationId, option.id, 'en', input.english.trim())
        }
        return updated
      },
      onSuccess: refresh,
    }),
    removeOption: useMutation({
      mutationFn: (input: { specificationId: string; optionId: string }) =>
        Category.removeOption(categoryId, input.specificationId, input.optionId),
      onSuccess: refresh,
    }),
  }
}
