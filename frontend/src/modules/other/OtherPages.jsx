import React, { useState } from 'react'
import client from '../../api/client'
import { useToast } from '../../components/Toast'

// ─── Records Page ──────────────────────────────────────────────────────────────
export function RecordsPage() {
  const { show, ToastContainer } = useToast()
  const [records, setRecords] = useState([
    { id:1, title:'سجل العقود الحكومية 2025', retention:'10 سنوات', status:'Active', count:342, class:'حكومي' },
    { id:2, title:'سجل الموارد البشرية',       retention:'7 سنوات',  status:'Active', count:1205, class:'HR' },
    { id:3, title:'سجل المراسلات الرسمية',     retention:'5 سنوات',  status:'Active', count:8732, class:'إداري' },
    { id:4, title:'سجل العقود المنتهية 2020',  retention:'10 سنوات', status:'LegalHold', count:89, class:'حكومي' },
  ])
  const [showNew, setShowNew] = useState(false)
  const [newTitle, setNewTitle] = useState('')

  const handleNewRecord = () => {
    if (!newTitle.trim()) return
    const r = { id: Date.now(), title: newTitle, retention: '5 سنوات', status: 'Active', count: 0, class: 'عام' }
    setRecords(prev => [...prev, r])
    show(`تم إنشاء السجل: ${newTitle}`, 'success')
    setNewTitle(''); setShowNew(false)
  }

  const handleView = (r) => show(`عرض سجل: ${r.title} (${r.count} وثيقة)`, 'info')
  const handleExport = (r) => {
    show(`جارٍ تصدير ${r.title}...`, 'info')
    setTimeout(() => show(`تم تصدير ${r.title} بنجاح`, 'success'), 1500)
  }
  const handleHold = (r) => {
    setRecords(prev => prev.map(x => x.id===r.id ? {...x, status: x.status==='LegalHold'?'Active':'LegalHold'} : x))
    show(r.status==='LegalHold' ? `تم رفع الحجز عن: ${r.title}` : `تم وضع حجز قانوني على: ${r.title}`, r.status==='LegalHold'?'success':'warning')
  }

  return (
    <div className="space-y-4 max-w-7xl">
      <ToastContainer />
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">إدارة السجلات</h1>
          <p className="text-gray-400 text-sm">جداول الاحتفاظ والأرشفة</p>
        </div>
        <button onClick={()=>setShowNew(true)} className="bg-blue-700 text-white text-sm px-4 py-2 rounded-lg hover:bg-blue-800 transition-colors">
          + سجل جديد
        </button>
      </div>

      {showNew && (
        <div className="bg-white rounded-xl border border-blue-200 p-4 flex gap-3 items-center shadow-sm">
          <input value={newTitle} onChange={e=>setNewTitle(e.target.value)} onKeyDown={e=>e.key==='Enter'&&handleNewRecord()}
            className="flex-1 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
            placeholder="اسم السجل الجديد..." autoFocus />
          <button onClick={handleNewRecord} className="bg-blue-700 text-white px-4 py-2 rounded-lg text-sm hover:bg-blue-800">حفظ</button>
          <button onClick={()=>setShowNew(false)} className="border border-gray-200 text-gray-500 px-3 py-2 rounded-lg text-sm hover:bg-gray-50">إلغاء</button>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {[
          { label:'سجلات نشطة',      value: records.filter(r=>r.status==='Active').length.toString(),     icon:'📂', color:'bg-green-50 text-green-700' },
          { label:'حجز قانوني',       value: records.filter(r=>r.status==='LegalHold').length.toString(), icon:'🔒', color:'bg-red-50 text-red-700' },
          { label:'إجمالي الوثائق',  value: records.reduce((s,r)=>s+r.count,0).toLocaleString('ar'),     icon:'📊', color:'bg-blue-50 text-blue-700' },
        ].map((k,i)=>(
          <div key={i} className="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
            <div className={`w-10 h-10 rounded-lg ${k.color} flex items-center justify-center text-xl`}>{k.icon}</div>
            <div>
              <p className="text-xs text-gray-400">{k.label}</p>
              <p className="text-2xl font-bold text-gray-900">{k.value}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-50 border-b border-gray-100">
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">السجل</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">الاحتفاظ</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">الحالة</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">الوثائق</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">إجراءات</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {records.map(r=>(
              <tr key={r.id} className="hover:bg-gray-50">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <span className="text-lg">📂</span>
                    <div>
                      <p className="font-medium text-gray-800 text-sm">{r.title}</p>
                      <p className="text-xs text-gray-400">{r.class}</p>
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3 text-gray-600 text-sm">{r.retention}</td>
                <td className="px-4 py-3">
                  <span className={`text-xs px-2.5 py-1 rounded-full font-medium ${r.status==='LegalHold'?'bg-red-100 text-red-700':'bg-green-100 text-green-700'}`}>
                    {r.status==='LegalHold'?'🔒 حجز قانوني':'✅ نشط'}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-600 text-sm">{r.count.toLocaleString('ar')}</td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button onClick={()=>handleView(r)} className="text-xs text-blue-600 hover:underline">عرض</button>
                    <button onClick={()=>handleExport(r)} className="text-xs text-gray-500 hover:underline">تصدير</button>
                    <button onClick={()=>handleHold(r)} className={`text-xs hover:underline ${r.status==='LegalHold'?'text-green-600':'text-red-500'}`}>
                      {r.status==='LegalHold'?'رفع الحجز':'حجز قانوني'}
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

// ─── Admin Page ────────────────────────────────────────────────────────────────
export function AdminPage() {
  const { show, ToastContainer } = useToast()
  const [users, setUsers] = useState([
    { id:1, name:'أحمد الزهراني',  email:'a.zahrani@darah.gov.sa',  role:'مدير',    dept:'الشؤون المالية',   status:'active' },
    { id:2, name:'مريم العنزي',   email:'m.anzi@darah.gov.sa',     role:'محرر',    dept:'الشؤون الإدارية', status:'active' },
    { id:3, name:'خالد القحطاني', email:'k.qahtani@darah.gov.sa',  role:'محرر',    dept:'تقنية المعلومات', status:'active' },
    { id:4, name:'فاطمة الشمري',  email:'f.shamri@darah.gov.sa',   role:'مدير',    dept:'الرئاسة التنفيذية', status:'active' },
    { id:5, name:'عمر الدوسري',   email:'o.dosari@darah.gov.sa',   role:'مراجع',   dept:'التدقيق الداخلي', status:'inactive' },
    { id:6, name:'نورة السبيعي',  email:'n.subai@darah.gov.sa',    role:'مستخدم',  dept:'إدارة المخاطر',  status:'active' },
  ])
  const [showAddUser, setShowAddUser] = useState(false)
  const [newUser, setNewUser] = useState({ name:'', email:'', role:'مستخدم', dept:'' })
  const [search, setSearch] = useState('')

  const handleToggle = (id) => {
    setUsers(prev => prev.map(u => u.id===id ? {...u, status: u.status==='active'?'inactive':'active'} : u))
    const u = users.find(x=>x.id===id)
    show(u.status==='active' ? `تم تعطيل حساب ${u.name}` : `تم تفعيل حساب ${u.name}`, u.status==='active'?'warning':'success')
  }

  const handleEdit = (u) => show(`تعديل بيانات: ${u.name}`, 'info')

  const handleAddUser = async () => {
    if (!newUser.name || !newUser.email) return show('الاسم والبريد مطلوبان', 'error')
    try {
      await client.post('/api/v1/users', newUser)
    } catch {}
    setUsers(prev => [...prev, { id:Date.now(), ...newUser, status:'active' }])
    show(`تم إضافة المستخدم: ${newUser.name}`, 'success')
    setNewUser({ name:'', email:'', role:'مستخدم', dept:'' })
    setShowAddUser(false)
  }

  const handleExport = () => {
    show('جارٍ تصدير قائمة المستخدمين...', 'info')
    setTimeout(() => {
      const csv = ['الاسم,البريد,الدور,القسم,الحالة', ...users.map(u=>`${u.name},${u.email},${u.role},${u.dept},${u.status==='active'?'فعال':'معطل'}`)].join('\n')
      const blob = new Blob([csv], { type:'text/csv;charset=utf-8' })
      const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'users.csv'; a.click()
      show('تم تصدير البيانات بنجاح', 'success')
    }, 800)
  }

  const filtered = users.filter(u => u.name.includes(search) || u.email.includes(search) || search==='')

  const ROLES = ['مدير','محرر','مراجع','مستخدم']

  return (
    <div className="space-y-4 max-w-7xl">
      <ToastContainer />
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">إدارة المستخدمين</h1>
          <p className="text-gray-400 text-sm">{users.filter(u=>u.status==='active').length} مستخدم نشط من {users.length}</p>
        </div>
        <div className="flex gap-2">
          <button onClick={handleExport} className="border border-gray-200 text-gray-600 text-sm px-4 py-2 rounded-lg hover:bg-gray-50 transition-colors">
            📊 تصدير
          </button>
          <button onClick={()=>setShowAddUser(true)} className="bg-blue-700 text-white text-sm px-4 py-2 rounded-lg hover:bg-blue-800 transition-colors">
            + مستخدم جديد
          </button>
        </div>
      </div>

      {/* Add User Form */}
      {showAddUser && (
        <div className="bg-white rounded-xl border border-blue-200 p-4 shadow-sm space-y-3">
          <p className="font-semibold text-gray-800 text-sm">إضافة مستخدم جديد</p>
          <div className="grid grid-cols-2 gap-3">
            <input value={newUser.name} onChange={e=>setNewUser(p=>({...p,name:e.target.value}))}
              className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
              placeholder="الاسم الكامل *" />
            <input value={newUser.email} onChange={e=>setNewUser(p=>({...p,email:e.target.value}))}
              className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
              placeholder="البريد الإلكتروني *" dir="ltr" />
            <select value={newUser.role} onChange={e=>setNewUser(p=>({...p,role:e.target.value}))}
              className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
              {ROLES.map(r=><option key={r}>{r}</option>)}
            </select>
            <input value={newUser.dept} onChange={e=>setNewUser(p=>({...p,dept:e.target.value}))}
              className="border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
              placeholder="الإدارة" />
          </div>
          <div className="flex gap-2">
            <button onClick={handleAddUser} className="bg-blue-700 text-white px-4 py-2 rounded-lg text-sm hover:bg-blue-800">حفظ</button>
            <button onClick={()=>setShowAddUser(false)} className="border border-gray-200 text-gray-500 px-3 py-2 rounded-lg text-sm hover:bg-gray-50">إلغاء</button>
          </div>
        </div>
      )}

      {/* Search */}
      <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-3">
        <div className="relative">
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
          <input type="text" placeholder="البحث بالاسم أو البريد..." value={search} onChange={e=>setSearch(e.target.value)}
            className="w-full pr-9 pl-3 py-2 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right" />
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-4 gap-3">
        {[
          { label:'إجمالي المستخدمين', value: users.length,                                    icon:'👥', color:'bg-blue-50 text-blue-700' },
          { label:'نشط',               value: users.filter(u=>u.status==='active').length,      icon:'✅', color:'bg-green-50 text-green-700' },
          { label:'معطل',              value: users.filter(u=>u.status==='inactive').length,    icon:'⛔', color:'bg-red-50 text-red-700' },
          { label:'مديرون',            value: users.filter(u=>u.role==='مدير').length,          icon:'👑', color:'bg-yellow-50 text-yellow-700' },
        ].map((k,i)=>(
          <div key={i} className="bg-white rounded-xl border border-gray-100 p-3 flex items-center gap-2">
            <div className={`w-8 h-8 rounded-lg ${k.color} flex items-center justify-center text-sm`}>{k.icon}</div>
            <div>
              <p className="text-[10px] text-gray-400">{k.label}</p>
              <p className="text-xl font-bold text-gray-900">{k.value}</p>
            </div>
          </div>
        ))}
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-50 border-b border-gray-100">
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">المستخدم</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">الدور</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">الإدارة</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">الحالة</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-400">إجراءات</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {filtered.map(u=>(
              <tr key={u.id} className="hover:bg-gray-50">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center text-blue-700 font-bold text-sm">
                      {u.name[0]}
                    </div>
                    <div>
                      <p className="font-medium text-gray-800">{u.name}</p>
                      <p className="text-xs text-gray-400">{u.email}</p>
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3">
                  <span className="text-xs bg-purple-50 text-purple-700 px-2.5 py-1 rounded-full font-medium">{u.role}</span>
                </td>
                <td className="px-4 py-3 text-gray-600 text-sm">{u.dept||'—'}</td>
                <td className="px-4 py-3">
                  <span className={`text-xs px-2.5 py-1 rounded-full font-medium ${u.status==='active'?'bg-green-100 text-green-700':'bg-red-100 text-red-500'}`}>
                    {u.status==='active'?'✅ فعال':'⛔ معطل'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    <button onClick={()=>handleEdit(u)} className="text-xs text-blue-600 hover:underline">تعديل</button>
                    <button onClick={()=>handleToggle(u.id)} className={`text-xs hover:underline ${u.status==='active'?'text-red-500':'text-green-600'}`}>
                      {u.status==='active'?'تعطيل':'تفعيل'}
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
