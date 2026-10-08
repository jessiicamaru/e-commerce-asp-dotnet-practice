import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '@ecommerce/core/constants/query-keys'
import { Category } from '@ecommerce/core/services/category'

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
