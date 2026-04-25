import { useLang } from '../i18n.js'
import React, { useState } from 'react'
import client from '../api/client'
import { useToast } from './Toast'

export function ChangePasswordModal({ onClose }) {
  const { lang, t } = useLang()
  const { show, ToastContainer } = useToast()
  const [form, setForm] = useState({ current: '', newPass: '', confirm: '' })
  const [loading, setLoading] = useState(false)
  const [strength, setStrength] = useState(0)
  const [showCurrent, setShowCurrent] = useState(false)
  const [showNew, setShowNew] = useState(false)

  const checkStrength = (pass) => {
    let s = 0
    if (pass.length >= 8) s++
    if (/[A-Z]/.test(pass)) s++
    if (/[a-z]/.test(pass)) s++
    if (/[0-9]/.test(pass)) s++
    if (/[^A-Za-z0-9]/.test(pass)) s++
    setStrength(s)
  }

  const strengthLabel = ['', t('pw_very_weak'), t('pw_weak'), t('pw_medium'), t('pw_strong'), t('pw_very_strong')]
  const strengthColor = ['', 'bg-red-500', 'bg-orange-500', 'bg-yellow-500', 'bg-blue-500', 'bg-green-500']

  const validate = () => {
    if (!form.current) return 'أدخل كلمة المرور الحالية'
    if (form.newPass.length < 8) return 'كلمة المرور يجب أن تكون 8 أحرف على الأقل'
    if (strength < 3) return 'كلمة المرور ضعيفة — يجب أن تحتوي على حروف وأرقام'
    if (form.newPass !== form.confirm) return 'كلمتا المرور غير متطابقتين'
    if (form.newPass === form.current) return 'كلمة المرور الجديدة يجب أن تختلف عن الحالية'
    return null
  }

  const handleSubmit = async () => {
    const err = validate()
    if (err) { show(err, 'error'); return }
    setLoading(true)
    try {
      await client.post('/api/v1/users/change-password', {
        currentPassword: form.current,
        newPassword: form.newPass
      })
      show('✅ تم تغيير كلمة المرور بنجاح', 'success')
      setTimeout(onClose, 1500)
    } catch (e) {
      show(e.response?.data?.message || 'كلمة المرور الحالية غير صحيحة', 'error')
    } finally { setLoading(false) }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <ToastContainer />
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md" onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div className="p-5 border-b border-gray-100 flex items-center gap-3">
          <div className="w-10 h-10 bg-blue-50 rounded-xl flex items-center justify-center text-xl">🔑</div>
          <div className="flex-1">
            <h2 className="font-bold text-gray-900">تغيير كلمة المرور</h2>
            <p className="text-xs text-gray-400 mt-0.5">يُنصح بتغييرها دورياً للحفاظ على أمان حسابك</p>
          </div>
          <button onClick={onClose} className="text-gray-300 hover:text-gray-600 text-xl w-8 h-8 flex items-center justify-center rounded-lg hover:bg-gray-100">✕</button>
        </div>

        <div className="p-5 space-y-4">
          {/* Current password */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">كلمة المرور الحالية <span className="text-red-400">*</span></label>
            <div className="relative">
              <input
                type={showCurrent ? 'text' : 'password'}
                value={form.current}
                onChange={e => setForm(p => ({...p, current: e.target.value}))}
                placeholder="••••••••"
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 pl-10"
                dir="ltr"
              />
              <button onClick={() => setShowCurrent(p => !p)}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-sm">
                {showCurrent ? '🙈' : '👁'}
              </button>
            </div>
          </div>

          {/* New password */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">كلمة المرور الجديدة <span className="text-red-400">*</span></label>
            <div className="relative">
              <input
                type={showNew ? 'text' : 'password'}
                value={form.newPass}
                onChange={e => { setForm(p => ({...p, newPass: e.target.value})); checkStrength(e.target.value) }}
                placeholder="••••••••"
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 pl-10"
                dir="ltr"
              />
              <button onClick={() => setShowNew(p => !p)}
                className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-sm">
                {showNew ? '🙈' : '👁'}
              </button>
            </div>
            {/* Strength indicator */}
            {form.newPass && (
              <div className="mt-2">
                <div className="flex gap-1 mb-1">
                  {[1,2,3,4,5].map(i => (
                    <div key={i} className={`flex-1 h-1.5 rounded-full transition-all ${i <= strength ? strengthColor[strength] : 'bg-gray-100'}`}/>
                  ))}
                </div>
                <p className={`text-[10px] font-medium ${strength >= 4 ? 'text-green-600' : strength >= 3 ? 'text-blue-600' : 'text-orange-600'}`}>
                  القوة: {strengthLabel[strength]}
                </p>
              </div>
            )}
            <p className="text-[10px] text-gray-400 mt-1">8 أحرف على الأقل، حروف كبيرة وصغيرة، أرقام، ورموز</p>
          </div>

          {/* Confirm */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">تأكيد كلمة المرور <span className="text-red-400">*</span></label>
            <div className="relative">
              <input
                type="password"
                value={form.confirm}
                onChange={e => setForm(p => ({...p, confirm: e.target.value}))}
                placeholder="••••••••"
                className={`w-full border rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 pr-9 ${
                  form.confirm && form.newPass !== form.confirm
                    ? 'border-red-300 focus:ring-red-400'
                    : form.confirm && form.newPass === form.confirm
                    ? 'border-green-300 focus:ring-green-400'
                    : 'border-gray-200 focus:ring-blue-400'
                }`}
                dir="ltr"
              />
              {form.confirm && (
                <span className="absolute right-3 top-1/2 -translate-y-1/2 text-sm">
                  {form.newPass === form.confirm ? '✅' : '❌'}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="p-5 border-t border-gray-100 flex gap-3">
          <button onClick={onClose} className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50 transition-colors">إلغاء</button>
          <button onClick={handleSubmit} disabled={loading}
            className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors">
            {loading ? t('pw_saving') : t('pw_change_btn')}
          </button>
        </div>
      </div>
    </div>
  )
}
