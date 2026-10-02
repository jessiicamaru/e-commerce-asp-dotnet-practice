import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Tailwind classes, merged so a later one wins. What every shadcn/ui component is built on. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
