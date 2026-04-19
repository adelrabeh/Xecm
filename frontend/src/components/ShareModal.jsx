import React, { useState } from 'react'
import { UserSearch } from './UserSearch'
import { useToast } from './Toast'

const PERMISSIONS = [
  { value:'read',    label:'قراءة فقط',    icon:'👁️', desc:'اطلاع وتنزيل فقط',        cls:'bg-gray-100 text-gray-700 border-gray-200' },
  { value:'comment', label:'قراءة وتعليق', icon:'💬', desc:'إضافة تعليقات',             cls:'bg-blue-50 text-blue-700 border-blue-200' },
  { value:'write',   label:'تحرير',         icon:'✏️', desc:'تعديل ورفع نسخ جديدة',    cls:'bg-yellow-50 text-yellow-700 border-yellow-200' },
  { value:'approve', label:'اعتماد',        icon:'✅', desc:'موافقة ورفض',              cls:'bg-green-50 text-green-700 border-green-200' },
]

const PERM_MAP = Object.fromEntries(PERMISSIONS.map(p => [p.value, p]))

export function ShareModal({ file, onClose }) {
  const { show, ToastContainer } = useToast()

  const [selectedUser, setSelectedUser]   = useState(null)
  const [permission, setPermission]       = useState('read')
  const [message, setMessage]             = useState('')
  const [notify, setNotify]               = useState(true)
  const [loading, setLoading]             = useState(false)
  const [shared, setShared]               = useState([])

  // All currently-shared userIds — to exclude from search
  const excludeIds = shared.map(s => s.userId)

  const handleShare = async () => {
    if (!selectedUser) { show('اختر موظفاً أولاً', 'error'); return }
    setLoading(true)

    // Simulate API call
    await new Promise(r => setTimeout(r, 500))

    setShared(prev => {
      const exists = prev.find(s => s.userId === selectedUser.userId)
      if (exists) return prev.map(s => s.userId === selectedUser.userId ? {...s, permission} : s)
      return [...prev, {
        userId: selectedUser.userId,
        name: selectedUser.fullNameAr,
        username: selectedUser.username,
        email: selectedUser.email,
        avatar: selectedUser.fullNameAr[0],
        permission,
      }]
    })

    show(`✅ تمت المشاركة مع ${selectedUser.fullNameAr} (${PERM_MAP[permission].label})`, 'success')
    setSelectedUser(null)   // clears UserSearch via useEffect
    setMessage('')
    setLoading(false)
  }

  const updatePermission = (userId, perm) => {
    setShared(prev => prev.map(s => s.userId === userId ? {...s, permission: perm} : s))
    show('تم تحديث الصلاحية', 'success')
  }

  const removeShare = (userId, name) => {
    setShared(prev => prev.filter(s => s.userId !== userId))
    show(`تم إلغاء مشاركة ${name}`, 'success')
  }

  const copyLink = () => {
    const url = `https://xecm-7nah.vercel.app/documents/${file?.id || 'doc'}`
    navigator.clipboard?.writeText(url)
      .then(() => show('✅ تم نسخ الرابط', 'success'))
      .catch(() => show('الرابط: ' + url, 'info'))
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <ToastContainer />
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="sticky top-0 bg-white p-4 border-b border-gray-100 flex items-center gap-3 z-10">
          <div className="w-9 h-9 bg-blue-50 rounded-xl flex items-center justify-center text-xl flex-shrink-0">🔗</div>
          <div className="flex-1 min-w-0">
            <h2 className="font-bold text-gray-900 text-sm">مشاركة الملف</h2>
            <p className="text-xs text-gray-400 truncate">{file?.name || file?.titleAr || 'الملف'}</p>
          </div>
          <button onClick={onClose} className="text-gray-300 hover:text-gray-600 text-xl w-8 h-8 flex items-center justify-center rounded-lg hover:bg-gray-100">✕</button>
        </div>

        <div className="p-4 space-y-4">

          {/* ── Search user ── */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-2">إضافة موظف</label>
            <UserSearch
              onSelect={setSelectedUser}
              selected={selectedUser}
              exclude={excludeIds}
              placeholder="ابحث بالاسم أو البريد..."
            />
          </div>

          {/* ── Permission selector (shown when user selected) ── */}
          {selectedUser && (
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-2">الصلاحية</label>
              <div className="grid grid-cols-2 gap-2">
                {PERMISSIONS.map(p => (
                  <button key={p.value} onClick={() => setPermission(p.value)}
                    className={`flex items-start gap-2 p-3 rounded-xl border-2 text-right transition-all ${
                      permission === p.value
                        ? `border-blue-500 bg-blue-50`
                        : 'border-gray-100 hover:border-gray-200 bg-white'
                    }`}>
                    <span className="text-base flex-shrink-0">{p.icon}</span>
                    <div>
                      <p className={`text-xs font-bold ${permission===p.value?'text-blue-700':'text-gray-700'}`}>{p.label}</p>
                      <p className="text-[10px] text-gray-400 mt-0.5">{p.desc}</p>
                    </div>
                    {permission === p.value && <span className="text-blue-500 text-sm mr-auto">✓</span>}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* ── Message (shown when user selected) ── */}
          {selectedUser && (
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-2">رسالة (اختياري)</label>
              <textarea
                value={message}
                onChange={e => setMessage(e.target.value)}
                rows={2}
                placeholder="أضف رسالة مع المشاركة..."
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"
              />
              <div className="flex items-center justify-between mt-2">
                <label className="flex items-center gap-2 text-xs text-gray-600 cursor-pointer">
                  <input type="checkbox" checked={notify} onChange={e=>setNotify(e.target.checked)} className="w-3.5 h-3.5 accent-blue-600 rounded"/>
                  إرسال إشعار
                </label>
                <button onClick={handleShare} disabled={loading}
                  className="bg-blue-700 text-white text-sm px-5 py-2 rounded-xl hover:bg-blue-800 disabled:opacity-50 flex items-center gap-2 transition-colors font-semibold">
                  {loading ? '⏳ جارٍ...' : '🔗 مشاركة'}
                </button>
              </div>
            </div>
          )}

          {/* ── Button when no user selected ── */}
          {!selectedUser && (
            <button onClick={handleShare} disabled
              className="w-full bg-gray-100 text-gray-400 text-sm py-2.5 rounded-xl cursor-not-allowed font-medium">
              اختر موظفاً للمشاركة
            </button>
          )}

          {/* ── Current shares ── */}
          {shared.length > 0 && (
            <div>
              <div className="flex items-center justify-between mb-2">
                <p className="text-xs font-bold text-gray-700">المشتركون ({shared.length})</p>
                <span className="text-[10px] text-gray-400">تغيير الصلاحية يُحفظ فوراً</span>
              </div>
              <div className="space-y-2">
                {shared.map(s => {
                  const perm = PERM_MAP[s.permission]
                  return (
                    <div key={s.userId} className="flex items-center gap-3 p-3 bg-gray-50 rounded-xl border border-gray-100">
                      <div className="w-8 h-8 bg-gradient-to-br from-blue-400 to-blue-600 rounded-full flex items-center justify-center text-white font-bold text-sm flex-shrink-0">
                        {s.avatar}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-semibold text-gray-800 truncate">{s.name}</p>
                        <p className="text-[10px] text-gray-400">{s.email}</p>
                      </div>
                      <select
                        value={s.permission}
                        onChange={e => updatePermission(s.userId, e.target.value)}
                        className={`text-xs border rounded-lg px-2 py-1 focus:outline-none focus:ring-2 focus:ring-blue-400 font-medium ${perm.cls}`}>
                        {PERMISSIONS.map(p => <option key={p.value} value={p.value}>{p.icon} {p.label}</option>)}
                      </select>
                      <button onClick={() => removeShare(s.userId, s.name)}
                        className="w-7 h-7 rounded-lg hover:bg-red-50 text-gray-300 hover:text-red-500 transition-colors flex items-center justify-center text-lg">
                        ✕
                      </button>
                    </div>
                  )
                })}
              </div>
            </div>
          )}

          {/* ── Empty state ── */}
          {shared.length === 0 && !selectedUser && (
            <div className="text-center py-6 text-gray-400">
              <div className="text-3xl mb-2">👥</div>
              <p className="text-sm">لم تُشارَك هذه الوثيقة مع أحد بعد</p>
              <p className="text-xs mt-1">ابحث عن موظف للمشاركة</p>
            </div>
          )}

          {/* ── Copy link ── */}
          <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-xl border border-dashed border-gray-200">
            <span className="text-gray-400">🔗</span>
            <p className="flex-1 text-xs text-gray-500 truncate font-mono">
              xecm-7nah.vercel.app/documents/{file?.id || '...'}
            </p>
            <button onClick={copyLink}
              className="text-xs text-blue-600 hover:text-blue-800 font-bold whitespace-nowrap flex-shrink-0 px-2 py-1 rounded-lg hover:bg-blue-50 transition-colors">
              نسخ
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
