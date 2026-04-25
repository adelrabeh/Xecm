import React, { useState, useEffect } from 'react'
import { useLang } from '../../i18n.js'
import { useLocalStorage } from '../../hooks/useLocalStorage'
import client from '../../api/client'
import { useToast } from '../../components/Toast'

// ─── Records Page ──────────────────────────────────────────────────────────────
export function RecordsPage() {
  const { show, ToastContainer } = useToast()
  const [records, setRecords] = useLocalStorage('ecm_records_admin', [
    { id:1, title:'سجل العقود الحكومية 2025', retention:'10 سنوات', status:'Active',     count:342,  class:'حكومي' },
    { id:2, title:'سجل الموارد البشرية',       retention:'7 سنوات',  status:'Active',     count:1205, class:'HR' },
    { id:3, title:'سجل المراسلات الرسمية',     retention:'5 سنوات',  status:'Active',     count:8732, class:'إداري' },
    { id:4, title:'سجل العقود المنتهية 2020',  retention:'10 سنوات', status:'LegalHold',  count:89,   class:'حكومي' },
  ])
  const [showNew, setShowNew]   = useState(false)
  const [newForm, setNewForm]   = useState({ title:'', retention:'5 سنوات', class:'عام' })

  const RETENTIONS = ['3 سنوات','5 سنوات','7 سنوات','10 سنوات','25 سنوات','دائم']
  const CLASSES    = ['عام','إداري','مالي','حكومي','HR','قانوني']

  const handleAdd = () => {
    if (!newForm.title.trim()) return
    setRecords(prev => [...prev, { id:Date.now(), ...newForm, status:'Active', count:0 }])
    show(`تم إنشاء سجل: ${newForm.title}`, 'success')
    setNewForm({ title:'', retention:'5 سنوات', class:'عام' })
    setShowNew(false)
  }

  const handleHold = (r) => {
    setRecords(prev => prev.map(x => x.id===r.id ? {...x, status:x.status==='LegalHold'?'Active':'LegalHold'} : x))
    show(r.status==='LegalHold' ? `رُفع الحجز عن: ${r.title}` : `حجز قانوني على: ${r.title}`, r.status==='LegalHold'?'success':'warning')
  }

  const handleExport = (r) => {
    show(`جارٍ تصدير ${r.title}...`, 'info')
    setTimeout(() => show(`✅ تم تصدير ${r.title}`, 'success'), 1200)
  }

  return (
    <div className="space-y-4 max-w-7xl">
      <ToastContainer />
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">إدارة السجلات</h1>
          <p className="text-gray-400 text-sm">جداول الاحتفاظ والأرشفة</p>
        </div>
        <button onClick={() => setShowNew(true)}
          className="bg-blue-700 text-white text-sm px-4 py-2 rounded-xl hover:bg-blue-800 transition-colors flex items-center gap-2">
          + سجل جديد
        </button>
      </div>

      {showNew && (
        <div className="bg-white rounded-2xl border-2 border-blue-200 p-4 space-y-3 shadow-sm">
          <p className="font-bold text-gray-800 text-sm">إنشاء سجل جديد</p>
          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-3">
              <input value={newForm.title} onChange={e=>setNewForm(p=>({...p,title:e.target.value}))}
                onKeyDown={e=>e.key==='Enter'&&handleAdd()}
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
                placeholder="اسم السجل *" autoFocus />
            </div>
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">فترة الاحتفاظ</label>
              <select value={newForm.retention} onChange={e=>setNewForm(p=>({...p,retention:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
                {RETENTIONS.map(r=><option key={r}>{r}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-gray-500 mb-1">التصنيف</label>
              <select value={newForm.class} onChange={e=>setNewForm(p=>({...p,class:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
                {CLASSES.map(c=><option key={c}>{c}</option>)}
              </select>
            </div>
            <div className="flex gap-2 items-end">
              <button onClick={handleAdd} className="flex-1 bg-blue-700 text-white py-2 rounded-xl text-sm hover:bg-blue-800 font-semibold">حفظ</button>
              <button onClick={()=>setShowNew(false)} className="border border-gray-200 text-gray-500 px-3 py-2 rounded-xl text-sm hover:bg-gray-50">إلغاء</button>
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-3 gap-4">
        {[
          { label:'سجلات نشطة',    value:records.filter(r=>r.status==='Active').length,    icon:'📂', color:'bg-green-50 text-green-700' },
          { label:'حجز قانوني',    value:records.filter(r=>r.status==='LegalHold').length,  icon:'🔒', color:'bg-red-50 text-red-700' },
          { label:'إجمالي الوثائق',value:records.reduce((s,r)=>s+r.count,0).toLocaleString('ar'), icon:'📊', color:'bg-blue-50 text-blue-700' },
        ].map((k,i)=>(
          <div key={i} className="bg-white rounded-2xl border border-gray-100 p-4 flex items-center gap-3">
            <div className={`w-10 h-10 rounded-xl ${k.color} flex items-center justify-center text-xl`}>{k.icon}</div>
            <div><p className="text-xs text-gray-400">{k.label}</p><p className="text-2xl font-bold text-gray-900">{k.value}</p></div>
          </div>
        ))}
      </div>

      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
        <table className="w-full text-sm">
          <thead><tr className="bg-gray-50 border-b border-gray-100">
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">السجل</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">الاحتفاظ</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">الحالة</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">الوثائق</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">إجراءات</th>
          </tr></thead>
          <tbody className="divide-y divide-gray-50">
            {records.map(r=>(
              <tr key={r.id} className="hover:bg-gray-50">
                <td className="px-4 py-3"><div className="flex items-center gap-2"><span>📂</span><div><p className="font-semibold text-gray-800">{r.title}</p><p className="text-xs text-gray-400">{r.class}</p></div></div></td>
                <td className="px-4 py-3 text-gray-600">{r.retention}</td>
                <td className="px-4 py-3"><span className={`text-xs px-2.5 py-1 rounded-full font-medium ${r.status==='LegalHold'?'bg-red-100 text-red-700':'bg-green-100 text-green-700'}`}>{r.status==='LegalHold'?'🔒 حجز قانوني':'✅ نشط'}</span></td>
                <td className="px-4 py-3 text-gray-600">{r.count.toLocaleString('ar')}</td>
                <td className="px-4 py-3"><div className="flex gap-3">
                  <button onClick={()=>show(`عرض: ${r.title}`, 'info')} className="text-xs text-blue-600 hover:underline">عرض</button>
                  <button onClick={()=>handleExport(r)} className="text-xs text-gray-500 hover:underline">تصدير</button>
                  <button onClick={()=>handleHold(r)} className={`text-xs hover:underline ${r.status==='LegalHold'?'text-green-600':'text-red-500'}`}>{r.status==='LegalHold'?'رفع الحجز':'حجز قانوني'}</button>
                </div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ─── Admin / Users Page ────────────────────────────────────────────────────────
const ROLES = ['مدير','محرر','مراجع','مستخدم','ضيف']
const DEPTS = ['الشؤون المالية','الشؤون الإدارية','تقنية المعلومات','الرئاسة التنفيذية','التدقيق الداخلي','إدارة المخاطر','الموارد البشرية','التحول الرقمي']

export function AdminPage() {
  const { show, ToastContainer } = useToast()
  const [users, setUsers] = useLocalStorage('ecm_users', [
    { id:1, name:'أحمد الزهراني',  email:'a.zahrani@darah.gov.sa',  role:'مدير',   dept:'الشؤون المالية',    status:'active', username:'a.zahrani' },
    { id:2, name:'مريم العنزي',   email:'m.anzi@darah.gov.sa',     role:'محرر',   dept:'الشؤون الإدارية',  status:'active', username:'m.anzi' },
    { id:3, name:'خالد القحطاني', email:'k.qahtani@darah.gov.sa',  role:'محرر',   dept:'تقنية المعلومات',  status:'active', username:'k.qahtani' },
    { id:4, name:'فاطمة الشمري',  email:'f.shamri@darah.gov.sa',   role:'مدير',   dept:'الرئاسة التنفيذية',status:'active', username:'f.shamri' },
    { id:5, name:'عمر الدوسري',   email:'o.dosari@darah.gov.sa',   role:'مراجع',  dept:'التدقيق الداخلي',  status:'inactive',username:'o.dosari' },
    { id:6, name:'نورة السبيعي',  email:'n.subai@darah.gov.sa',    role:'مستخدم', dept:'إدارة المخاطر',    status:'active', username:'n.subai' },
  ])

  const [search, setSearch]       = useState('')
  const [showAdd, setShowAdd]     = useState(false)
  const [editUser, setEditUser]   = useState(null)
  const [newUser, setNewUser]     = useState({ name:'', email:'', username:'', role:'مستخدم', dept:'', password:'' })
  const [loading, setLoading]     = useState(false)
  const [filterRole, setFilterRole] = useState('all')

  useEffect(() => {
    client.get('/api/v1/users')
      .then(r => {
        const d = r.data?.data || r.data
        if (Array.isArray(d) && d.length > 0)
          setUsers(d.map(u => ({
            id: u.userId, name: u.fullNameAr || u.username,
            email: u.email, role: 'مستخدم',
            dept: u.jobTitle || '—',
            status: u.isActive ? 'active' : 'inactive',
            username: u.username,
          })))
      }).catch(() => {}) // silently use mock data if API fails
  }, [])

  const handleAdd = async () => {
    if (!newUser.name.trim() || !newUser.email.trim()) { show('الاسم والبريد مطلوبان', 'error'); return }
    if (!newUser.username.trim()) { show('اسم المستخدم مطلوب', 'error'); return }
    setLoading(true)
    try {
      await client.post('/api/v1/users', { username: newUser.username, email: newUser.email, fullNameAr: newUser.name })
    } catch {}
    setUsers(prev => [...prev, { id:Date.now(), ...newUser, status:'active' }])
    show(`✅ تم إضافة المستخدم: ${newUser.name}`, 'success')
    setNewUser({ name:'', email:'', username:'', role:'مستخدم', dept:'', password:'' })
    setShowAdd(false)
    setLoading(false)
  }

  const handleEdit = (u) => { setEditUser({...u}); setShowAdd(false) }

  const handleSaveEdit = () => {
    setUsers(prev => prev.map(u => u.id === editUser.id ? editUser : u))
    show(`✅ تم حفظ تعديلات: ${editUser.name}`, 'success')
    setEditUser(null)
  }

  const handleToggle = (id) => {
    const u = users.find(x => x.id === id)
    setUsers(prev => prev.map(x => x.id===id ? {...x, status:x.status==='active'?'inactive':'active'} : x))
    show(u.status==='active' ? `تم تعطيل ${u.name}` : `تم تفعيل ${u.name}`, u.status==='active'?'warning':'success')
  }

  const handleGrantAccess = (u) => {
    setUsers(prev => prev.map(x => x.id===u.id ? {...x, fullAccess:!x.fullAccess} : x))
    // Save permission to user's localStorage permissions
    try {
      const key = 'ecm_user_perms_' + u.username
      const perms = JSON.parse(localStorage.getItem(key)||'[]')
      if (!u.fullAccess) {
        localStorage.setItem(key, JSON.stringify([...perms, 'documents.all']))
        show(`✅ منح ${u.name} صلاحية البحث في كل الملفات`, 'success')
      } else {
        localStorage.setItem(key, JSON.stringify(perms.filter(p=>p!=='documents.all')))
        show(`تم سحب صلاحية الوصول الكامل من ${u.name}`, 'warning')
      }
    } catch {}
  }

  const handleExport = () => {
    const csv = ['الاسم,البريد,المستخدم,الدور,القسم,الحالة',
      ...users.map(u=>`${u.name},${u.email},${u.username},${u.role},${u.dept||'—'},${u.status==='active'?'فعال':'معطل'}`)
    ].join('\n')
    const blob = new Blob(['\ufeff'+csv], {type:'text/csv;charset=utf-8'})
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'users.csv'; a.click()
    show('✅ تم تصدير البيانات', 'success')
  }

  const filtered = users.filter(u =>
    (u.name.includes(search) || u.email.includes(search) || u.username?.includes(search) || !search) &&
    (filterRole === 'all' || u.role === filterRole)
  )

  // FormField moved outside component to prevent focus loss

  return (
    <div className="space-y-4 max-w-7xl">
      <ToastContainer />

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">t('users_title')</h1>
          <p className="text-gray-400 text-sm">{users.filter(u=>u.status==='active').length} نشط من {users.length}</p>
        </div>
        <div className="flex gap-2">
          <button onClick={handleExport} className="border border-gray-200 text-gray-600 text-sm px-4 py-2 rounded-xl hover:bg-gray-50 transition-colors">📊 تصدير CSV</button>
          <button onClick={()=>{setShowAdd(true);setEditUser(null)}} className="bg-blue-700 text-white text-sm px-4 py-2 rounded-xl hover:bg-blue-800 transition-colors flex items-center gap-1.5">
            + t('new_user')
          </button>
        </div>
      </div>

      {/* Add User Form */}
      {showAdd && !editUser && (
        <div className="bg-white rounded-2xl border-2 border-blue-200 p-5 shadow-sm space-y-4">
          <div className="flex items-center justify-between">
            <p className="font-bold text-gray-800">إضافة t('new_user')</p>
            <button onClick={()=>setShowAdd(false)} className="text-gray-400 hover:text-gray-600">✕</button>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">الاسم الكامل <span className="text-red-400">*</span></label>
              <input value={newUser.name} onChange={e=>setNewUser(p=>({...p,name:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right" placeholder="الاسم الكامل"/>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">اسم المستخدم <span className="text-red-400">*</span></label>
              <input value={newUser.username} onChange={e=>setNewUser(p=>({...p,username:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400" placeholder="username" dir="ltr"/>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">البريد الإلكتروني <span className="text-red-400">*</span></label>
              <input type="email" value={newUser.email} onChange={e=>setNewUser(p=>({...p,email:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400" placeholder="email@domain.com" dir="ltr"/>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">كلمة المرور</label>
              <input type="password" value={newUser.password} onChange={e=>setNewUser(p=>({...p,password:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400" dir="ltr"/>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">الدور</label>
              <select value={newUser.role} onChange={e=>setNewUser(p=>({...p,role:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
                {ROLES.map(r=><option key={r}>{r}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">الإدارة / القسم</label>
              <select value={newUser.dept} onChange={e=>setNewUser(p=>({...p,dept:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
                <option value="">— اختر القسم —</option>
                {DEPTS.map(d=><option key={d}>{d}</option>)}
              </select>
            </div>
          </div>
          <div className="flex gap-2">
            <button onClick={handleAdd} disabled={loading}
              className="bg-blue-700 text-white px-6 py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-50">
              {loading ? '⏳' : '✅'} حفظ المستخدم
            </button>
            <button onClick={()=>setShowAdd(false)} className="border border-gray-200 text-gray-600 px-4 py-2.5 rounded-xl text-sm hover:bg-gray-50">إلغاء</button>
          </div>
        </div>
      )}

      {/* Edit User Form */}
      {editUser && (
        <div className="bg-white rounded-2xl border-2 border-orange-200 p-5 shadow-sm space-y-4">
          <div className="flex items-center justify-between">
            <p className="font-bold text-gray-800">تعديل: {editUser.name}</p>
            <button onClick={()=>setEditUser(null)} className="text-gray-400 hover:text-gray-600">✕</button>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <FormField label="الاسم الكامل" value={editUser.name}
              onChange={e=>setEditUser(p=>({...p,name:e.target.value}))} />
            <FormField label="البريد الإلكتروني" value={editUser.email}
              onChange={e=>setEditUser(p=>({...p,email:e.target.value}))} />
            <FormField label="الدور">
              <select value={editUser.role} onChange={e=>setEditUser(p=>({...p,role:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400">
                {ROLES.map(r=><option key={r}>{r}</option>)}
              </select>
            </FormField>
            <FormField label="الإدارة / القسم">
              <select value={editUser.dept} onChange={e=>setEditUser(p=>({...p,dept:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400">
                <option value="">— اختر القسم —</option>
                {DEPTS.map(d=><option key={d}>{d}</option>)}
              </select>
            </FormField>
          </div>
          <div className="flex gap-2">
            <button onClick={handleSaveEdit} className="bg-orange-500 text-white px-6 py-2.5 rounded-xl text-sm font-bold hover:bg-orange-600">💾 حفظ التعديلات</button>
            <button onClick={()=>setEditUser(null)} className="border border-gray-200 text-gray-600 px-4 py-2.5 rounded-xl text-sm hover:bg-gray-50">إلغاء</button>
          </div>
        </div>
      )}

      {/* Stats */}
      <div className="grid grid-cols-4 gap-3">
        {[
          { label:'الإجمالي',  value:users.length,                               icon:'👥', c:'bg-blue-50 text-blue-700' },
          { label:'نشط',       value:users.filter(u=>u.status==='active').length, icon:'✅', c:'bg-green-50 text-green-700' },
          { label:'معطل',      value:users.filter(u=>u.status==='inactive').length,icon:'⛔',c:'bg-red-50 text-red-700' },
          { label:'مديرون',    value:users.filter(u=>u.role==='مدير').length,     icon:'👑', c:'bg-yellow-50 text-yellow-700' },
        ].map((k,i)=>(
          <div key={i} className={`${k.c.split(' ')[0]} rounded-2xl p-3 flex items-center gap-2.5 border ${k.c.split(' ')[0].replace('bg','border').replace('50','200')}`}>
            <span className="text-2xl">{k.icon}</span>
            <div><p className="text-[10px] text-gray-500">{k.label}</p><p className={`text-2xl font-black ${k.c.split(' ')[1]}`}>{k.value}</p></div>
          </div>
        ))}
      </div>

      {/* Search + filter */}
      <div className="bg-white rounded-2xl border border-gray-100 p-3 flex gap-3">
        <div className="relative flex-1">
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
          <input value={search} onChange={e=>setSearch(e.target.value)}
            placeholder="البحث بالاسم أو البريد أو المستخدم..."
            className="w-full pr-9 pl-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
        </div>
        <select value={filterRole} onChange={e=>setFilterRole(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 text-sm text-gray-600 focus:outline-none">
          <option value="all">كل الأدوار</option>
          {ROLES.map(r=><option key={r} value={r}>{r}</option>)}
        </select>
      </div>

      {/* Table */}
      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
        <table className="w-full text-sm">
          <thead><tr className="bg-gray-50 border-b border-gray-100">
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">المستخدم</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">الدور</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400 hidden md:table-cell">القسم</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">الحالة</th>
            <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">إجراءات</th>
          </tr></thead>
          <tbody className="divide-y divide-gray-50">
            {filtered.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-12 text-center text-gray-400"><div className="text-3xl mb-2">👤</div><p>لا توجد نتائج</p></td></tr>
            )}
            {filtered.map(u=>(
              <tr key={u.id} className={`hover:bg-gray-50 transition-colors ${editUser?.id===u.id?'bg-orange-50':''}`}>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div className={`w-9 h-9 rounded-full flex items-center justify-center font-bold text-sm flex-shrink-0 ${u.status==='active'?'bg-blue-100 text-blue-700':'bg-gray-100 text-gray-400'}`}>
                      {u.name[0]}
                    </div>
                    <div>
                      <p className="font-semibold text-gray-800">{u.name}</p>
                      <p className="text-xs text-gray-400">{u.email}</p>
                      {u.username && <p className="text-[10px] text-gray-300">@{u.username}</p>}
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3">
                  <span className="text-xs bg-purple-50 text-purple-700 border border-purple-200 px-2.5 py-1 rounded-full font-medium">{u.role}</span>
                </td>
                <td className="px-4 py-3 text-gray-500 text-xs hidden md:table-cell">{u.dept||'—'}</td>
                <td className="px-4 py-3">
                  <span className={`text-xs px-2.5 py-1 rounded-full font-medium ${u.status==='active'?'bg-green-100 text-green-700':'bg-red-100 text-red-500'}`}>
                    {u.status==='active'?'✅ فعال':'⛔ معطل'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button onClick={()=>handleEdit(u)} className="text-xs text-blue-600 hover:text-blue-800 font-medium hover:underline">تعديل</button>
                    <button onClick={()=>handleToggle(u.id)} className={`text-xs font-medium hover:underline ${u.status==='active'?'text-red-500 hover:text-red-700':'text-green-600 hover:text-green-800'}`}>
                      {u.status==='active'?'تعطيل':'تفعيل'}
                    </button>
                    <button onClick={()=>handleGrantAccess(u)} className={`text-xs font-medium hover:underline ${u.fullAccess?'text-purple-700':'text-gray-400 hover:text-purple-600'}`} title="صلاحية البحث في كل الملفات">
                      {u.fullAccess?'🔓 وصول كامل':'🔐 منح وصول'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
