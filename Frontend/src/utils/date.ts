import { getLocale } from '@/i18n'

type DateInput = string | number | Date | null | undefined

const toValidDate = (value: DateInput): Date | null => {
  if (value === null || value === undefined || value === '') return null
  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

export const formatLocalizedDate = (value: DateInput, fallback = '--', options?: Intl.DateTimeFormatOptions) => {
  const date = toValidDate(value)
  return date ? date.toLocaleDateString(getLocale(), options) : fallback
}

export const formatLocalizedDateTime = (value: DateInput, fallback = '--', options?: Intl.DateTimeFormatOptions) => {
  const date = toValidDate(value)
  return date ? date.toLocaleString(getLocale(), options) : fallback
}

export const formatLocalizedTime = (value: DateInput, fallback = '--', options?: Intl.DateTimeFormatOptions) => {
  const date = toValidDate(value)
  return date ? date.toLocaleTimeString(getLocale(), options) : fallback
}
