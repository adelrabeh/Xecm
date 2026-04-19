import React, { useState } from 'react'
import { UserSearch } from './UserSearch'

const PERMISSIONS = [
  { value:'read',     label:'قراءة فقط',        icon:'👁️',  desc:'يمكن الاطلاع وتنزيل الملف' },
  { value:'comment',  label:'قراءة وتعليق',     icon:'💬',  desc:'يمكن إضافة تعليقات' },
  { value:'write',    label:'تحرير',             icon:'✏️',  desc:'يمكن التعديل والرفع' },
  { value:'approve',  label:'اعتماد',            icon:'✅',  desc:'يمكن الموافقة والرفض' },
]

export function ShareModal({ file, onClose, show }) {
  const [selectedUser, setSelectedUser] = useState(null)
  const [permission, setPermission]     = useState('read')
  const [message, setMessage]           = useState('')
  const [notify, setNotify]             = useState(true)
  const [shared, setShared]             = useState([
    { userId:2, name:'أحمد الزهراني',  permission:'read',    avatar:'أ' },
    { userId:3, name:'مريم العنزي',   permission:'comment', avatar:'م' },
  ])
  const [loading, setLoading] = useState(false)

  const handleShare = () => {
    if (!selectedUser) { show('يجب اختيار موظف', 'error'); return }
    setLoading(true)
    setTimeout(() => {
      setShared(prev => {
        const exists = prev.find(s => s.userId === selectedUser.userId)
        if (exists) return prev.map(s => s.userId===selectedUser.userId ? {...s, permission} : s)
        return [...prev, { userId:selectedUser.userId, name:selectedUser.fullNameAr, permission, avatar:selectedUser.fullNameAr[0] }]
      })
      show(`تمت مشاركة الملف مع ${selectedUser.fullNameAr}`, 'success')
      setSelectedUser(null); setMessage(''); setLoading(false)
    }, 600)
  }

  const removeShare = (userId) => {
    setShared(prev => prev.filter(s => s.userId !== userId))
    show('تم إلغاء المشاركة', 'success')
  }

  const copyLink = () => {
    navigator.clipboard?.writeText(`https://xecm-7nah.vercel.app/documents/${file?.id}`)
      .then(() => show('تم نسخ رابط الملف', 'success'))
      .catch(() => show('رابط: ' + file?.id, 'info'))
  }

  const PERM_CLS = { read:'bg-gray-100 text-gray-600', comment:'bg-blue-100 text-blue-600', write:'bg-yellow-100 text-yellow-700', approve:'bg-green-100 text-green-700' }
  const PERM_LBL = { read:'قراءة', comment:'تعليق', write:'تحرير', approve:'اعتماد' }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg" onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div className="p-5 border-b border-gray-100 flex items-start justify-between">
          <div>
            <h2 className="font-bold text-gray-900">مشاركة الملف</h2>
            <p className="text-xs text-gray-400 mt-0.5 truncate max-w-sm">{file?.name}</p>
          </div>
          <button onClick={onClose} className="text-gray-300 hover:text-gray-500 text-xl mt-0.5">✕</button>
        </div>

        <div className="p-5 space-y-5">
          {/* Add user */}
          <div className="space-y-3">
            <label className="block text-xs font-bold text-gray-700">إضافة موظف</label>
            <UserSearch onSelect={setSelectedUser} selected={selectedUser} placeholder="ابحث باسم الموظف أو بريده..." />

            {/* Permissions */}
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-2">الصلاحية</label>
              <div className="grid grid-cols-2 gap-2">
                {PERMISSIONS.map(p => (
                  <button key={p.value} onClick={() => setPermission(p.value)}
                    className={`flex items-start gap-2 p-2.5 rounded-xl border-2 text-right transition-all ${permission===p.value?'border-blue-500 bg-blue-50':'border-gray-100 hover:border-gray-300'}`}>
                    <span className="text-lg flex-shrink-0 mt-0.5">{p.icon}</span>
                    <div>
                      <p className={`text-xs font-semibold ${permission===p.value?'text-blue-700':'text-gray-700'}`}>{p.label}</p>
                      <p className="text-[10px] text-gray-400">{p.desc}</p>
                    </div>
                  </button>
                ))}
              </div>
            </div>

            {/* Message */}
            <textarea value={message} onChange={e=>setMessage(e.target.value)} rows={2}
              placeholder="رسالة اختيارية مع المشاركة..."
              className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"/>

            <div className="flex items-center justify-between">
              <label className="flex items-center gap-2 text-xs text-gray-600 cursor-pointer">
                <input type="checkbox" checked={notify} onChange={e=>setNotify(e.target.checked)} className="w-4 h-4 accent-blue-600 rounded"/>
                إرسال إشعار للموظف
              </label>
              <button onClick={handleShare} disabled={loading||!selectedUser}
                className="bg-blue-700 text-white text-sm px-5 py-2 rounded-xl hover:bg-blue-800 disabled:opacity-50 flex items-center gap-2 transition-colors">
                {loading ? '⏳' : '🔗'} مشاركة
              </button>
            </div>
          </div>

          {/* Current shares */}
          {shared.length > 0 && (
            <div>
              <p className="text-xs font-bold text-gray-700 mb-2">المشتركون الحاليون ({shared.length})</p>
              <div className="space-y-2">
                {shared.map(s => (
                  <div key={s.userId} className="flex items-center gap-3 p-2.5 bg-gray-50 rounded-xl">
                    <div className="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0">
                      {s.avatar}
                    </div>
                    <div className="flex-1">
                      <p className="text-sm font-medium text-gray-800">{s.name}</p>
                    </div>
                    <select value={s.permission}
                      onChange={e => setShared(prev=>prev.map(x=>x.userId===s.userId?{...x,permission:e.target.value}:x))}
                      className="text-xs border border-gray-200 rounded-lg px-2 py-1 focus:outline-none bg-white">
                      {PERMISSIONS.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}
                    </select>
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${PERM_CLS[s.permission]}`}>
                      {PERM_LBL[s.permission]}
                    </span>
                    <button onClick={() => removeShare(s.userId)} className="text-gray-300 hover:text-red-500 transition-colors text-lg">✕</button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Copy link */}
          <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-xl border border-dashed border-gray-200">
            <span className="text-gray-400 text-lg">🔗</span>
            <p className="flex-1 text-xs text-gray-500 truncate">رابط مباشر للملف</p>
            <button onClick={copyLink} className="text-xs text-blue-600 hover:underline font-medium flex-shrink-0">نسخ الرابط</button>
          </div>
        </div>
      </div>
    </div>
  )
}
