import { useLocalStorage } from './useLocalStorage'

// Shared key between DocumentsPage uploads and LibraryPage
const LIBRARY_KEY = 'ecm_library_files'

export function useLibraryFiles() {
  return useLocalStorage(LIBRARY_KEY, [])
}

export function addToLibrary(file) {
  try {
    const existing = JSON.parse(localStorage.getItem(LIBRARY_KEY) || '[]')
    const updated = [file, ...existing.filter(f => f.id !== file.id)]
    localStorage.setItem(LIBRARY_KEY, JSON.stringify(updated))
  } catch {}
}
