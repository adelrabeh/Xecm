import React, { useEffect } from 'react'

export function Toast({ message, type = 'success', onClose }) {
  useEffect(() => {
    const t = setTimeout(onClose, 3500)
    return () => clearTimeout(t)
  }, [onClose])

  const styles = {
    success: 'bg-green-600',
    error:   'bg-red-600',
    info:    'bg-blue-600',
    warning: 'bg-yellow-500',
  }

  return (
    <div className={`fixed bottom-6 left-1/2 -translate-x-1/2 z-50 flex items-center gap-3 px-5 py-3 rounded-xl text-white text-sm shadow-2xl ${styles[type]} animate-fade-in`}>
      <span>{type==='success'?'✅':type==='error'?'❌':type==='warning'?'⚠️':'ℹ️'}</span>
      <span>{message}</span>
      <button onClick={onClose} className="mr-2 opacity-70 hover:opacity-100">✕</button>
    </div>
  )
}

export function useToast() {
  const [toasts, setToasts] = React.useState([])
  const show = (message, type='success') => {
    const id = Date.now()
    setToasts(t => [...t, { id, message, type }])
  }
  const remove = (id) => setToasts(t => t.filter(x => x.id !== id))
  const ToastContainer = () => (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 flex flex-col gap-2">
      {toasts.map(t => <Toast key={t.id} {...t} onClose={() => remove(t.id)} />)}
    </div>
  )
  return { show, ToastContainer }
}
