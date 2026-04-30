import { useState, useEffect, useCallback } from 'react'

export function useLocalStorage(key, defaultValue) {
  const [value, setValue] = useState(() => {
    try {
      const stored = localStorage.getItem(key)
      if (stored === null || stored === 'undefined') return defaultValue
      const parsed = JSON.parse(stored)
      return parsed !== null && parsed !== undefined ? parsed : defaultValue
    } catch {
      return defaultValue
    }
  })

  // Write to localStorage immediately + on value change
  const setValueAndPersist = useCallback((updater) => {
    setValue(prev => {
      const next = typeof updater === 'function' ? updater(prev) : updater
      try {
        localStorage.setItem(key, JSON.stringify(next))
      } catch {}
      return next
    })
  }, [key])

  // Also sync on mount / key change
  useEffect(() => {
    try {
      localStorage.setItem(key, JSON.stringify(value))
    } catch {}
  }, [key, value])

  return [value, setValueAndPersist]
}
