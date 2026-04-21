// In-memory store for uploaded file blobs (session only - can't persist binary in localStorage)
const fileStore = new Map() // fileId → { url: blobURL, file: File object, type: string }

export function storeFile(fileId, file) {
  try {
    const url = URL.createObjectURL(file)
    fileStore.set(fileId, { url, file, type: file.type, name: file.name })
    return url
  } catch { return null }
}

export function getFileBlob(fileId) {
  return fileStore.get(fileId) || null
}

export function revokeFile(fileId) {
  const stored = fileStore.get(fileId)
  if (stored?.url) URL.revokeObjectURL(stored.url)
  fileStore.delete(fileId)
}
