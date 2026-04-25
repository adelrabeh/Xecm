import { useLang } from '../i18n.js'
import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

export default function LoginPage() {
  const { lang, setLang, t, isRTL } = useLang()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const { login, isLoading, error } = useAuthStore()
  const navigate = useNavigate()

  const handleSubmit = async (e) => {
    e.preventDefault()
    const ok = await login(username, password)
    if (ok) navigate('/dashboard')
  }

  return (
    <div dir={isRTL?'rtl':'ltr'} dir={isRTL?'rtl':'ltr'} className="min-h-screen bg-gradient-to-br from-gov-900 to-primary-700 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md p-8">
        <div className="text-center mb-8">
          <div className="w-16 h-16 bg-primary-500 rounded-2xl flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8 text-white" fill="currentColor" viewBox="0 0 24 24">
              <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zm-1 1.5L18.5 9H13V3.5zM6 20V4h5v7h7v9H6z"/>
            </svg>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">{t('org_name')}</h1>
          <p className="text-gray-500 text-sm mt-1">{t('sign_in_subtitle')}</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg p-3 text-center">
              {error}
            </div>
          )}

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">{t('username')}</label>
            <input
              type="text"
              value={username}
              onChange={e => setUsername(e.target.value)}
              className="w-full px-4 py-2.5 border border-gray-300 rounded-lg text-right focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
              placeholder="أدخل {t('username')}"
              required
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">{t('password')}</label>
            <input
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              className="w-full px-4 py-2.5 border border-gray-300 rounded-lg text-right focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
              placeholder="أدخل {t('password')}"
              required
            />
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full bg-primary-700 hover:bg-primary-900 text-white font-semibold py-2.5 rounded-lg transition-colors disabled:opacity-50"
          >
            {isLoading ? 'جارٍ التحقق...' : 'تسجيل الدخول'}
          </button>
        </form>

        <p className="text-center text-xs text-gray-400 mt-6">
          DARAH ECM v1.0 — جميع الحقوق محفوظة
        </p>
      </div>
    </div>
  )
}
