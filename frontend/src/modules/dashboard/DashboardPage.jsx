import React, { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import client from '../../api/client'
import { useToast } from '../../components/Toast'
import { UploadModal } from '../../components/UploadModal'

// ─── Personal mock data ───────────────────────────────────────────────────────
const MY_TASKS = [
  { id:1, title:'مراجعة عقد التوريد السنوي',     type:'اعتماد', due:'2026-04-14', priority:'عاجل', from:'أحمد المطيري',  isOverdue:true  },
  { id:2, title:'اعتماد تقرير الميزانية Q1',      type:'مراجعة', due:'2026-04-22', priority:'مهم',  from:'مريم العتيبي',  isOverdue:false },
  { id:3, title:'التحقق من سياسة الخصوصية',      type:'اعتماد', due:'2026-04-28', priority:'عادي', from:'النظام',        isOverdue:false },
  { id:4, title:'مراجعة خطة الاستمرارية التشغيلية',type:'مراجعة',due:'2026-04-12', priority:'عاجل', from:'نورة السبيعي', isOverdue:true  },
]

const MY_DOCS = [
  { id:'d1', name:'تقرير الميزانية السنوي 2026.pdf', icon:'📕', type:'PDF',  modified:'منذ ساعة',    status:'Approved',    size:'2.4 MB' },
  { id:'d2', name:'عقد توريد المستلزمات.docx',       icon:'📘', type:'DOCX', modified:'منذ 3 ساعات', status:'UnderReview', size:'1.2 MB' },
  { id:'d3', name:'سياسة حماية البيانات.pdf',        icon:'📕', type:'PDF',  modified:'أمس',          status:'Active',      size:'0.9 MB' },
  { id:'d4', name:'خطة الاستمرارية التشغيلية.docx',  icon:'📘', type:'DOCX', modified:'منذ يومين',   status:'Draft',       size:'1.8 MB' },
  { id:'d5', name:'عرض الرؤية الاستراتيجية.pptx',   icon:'📙', type:'PPTX', modified:'منذ 3 أيام',  status:'Active',      size:'8.2 MB' },
]

const MY_WORKFLOWS = [
  { id:1, title:'سير اعتماد عقد التوريد', status:'Active',    progress:33,  steps:3, done:1, started:'2026-04-10', priority:'عاجل' },
  { id:2, title:'مراجعة التقرير السنوي',  status:'Active',    progress:67,  steps:3, done:2, started:'2026-04-08', priority:'مهم'  },
  { id:3, title:'اعتماد الميزانية 2026',  status:'Completed', progress:100, steps:3, done:3, started:'2026-03-20', priority:'عادي' },
]

const MY_FAVORITES = [
  { id:'d1', name:'تقرير الميزانية',          icon:'📕', type:'PDF',  category:'المالية' },
  { id:'d3', name:'سياسة حماية البيانات',    icon:'📕', type:'PDF',  category:'الحوكمة' },
  { id:'d8', name:'عرض الرؤية الاستراتيجية', icon:'📙', type:'PPTX', category:'الاستراتيجية' },
]

const MY_ACTIVITY = [
  { action:'رفعت وثيقة',    doc:'تقرير الميزانية Q1',     time:'منذ ساعة',    icon:'⬆️' },
  { action:'وافقت على',     doc:'عقد التوريد السنوي',     time:'منذ 3 ساعات', icon:'✅' },
  { action:'عدّلت',         doc:'سياسة حماية البيانات',   time:'أمس',          icon:'✏️' },
  { action:'شاركت',         doc:'عرض الرؤية الاستراتيجية',time:'منذ يومين',   icon:'🔗' },
  { action:'طلبت اعتماد',   doc:'خطة الاستمرارية',        time:'منذ 3 أيام',  icon:'📤' },
]

const NOTIFS = [
  { id:1, text:'طلب موافقة: عقد التوريد السنوي من أحمد المطيري', time:'منذ 5 دقائق', type:'task',    read:false },
  { id:2, text:'تمت الموافقة على طلبك: تقرير الميزانية Q1',       time:'منذ ساعة',    type:'approve', read:false },
  { id:3, text:'تذكير: مهمة متأخرة — مراجعة عقد التوريد',        time:'منذ ساعتين', type:'warning', read:false },
  { id:4, text:'شارك معك خالد القحطاني: سياسة البيانات',          time:'أمس',          type:'share',   read:true  },
]

const STATUS_CLS = { Active:'bg-green-100 text-green-700', Approved:'bg-blue-100 text-blue-700', Draft:'bg-purple-100 text-purple-700', Completed:'bg-green-100 text-green-700', UnderReview:'bg-yellow-100 text-yellow-700' }
const STATUS_LBL = { Active:'نشط', Approved:'معتمد', Draft:'مسودة', Completed:'مكتمل', UnderReview:'قيد المراجعة' }
const PRIO_CLS   = { 'عاجل':'bg-red-100 text-red-700', 'مهم':'bg-yellow-100 text-yellow-700', 'عادي':'bg-gray-100 text-gray-500' }
const PRIO_BAR   = { 'عاجل':'bg-red-400', 'مهم':'bg-yellow-400', 'عادي':'bg-gray-200' }
const NOTIF_ICON = { task:'📋', approve:'✅', warning:'⚠️', share:'🔗' }

export default function DashboardPage() {
  const { user }   = useAuthStore()
  const navigate   = useNavigate()
  const { show, ToastContainer } = useToast()

  const [tasks, setTasks]         = useState(MY_TASKS)
  const [docs, setDocs]           = useState(MY_DOCS)
  const [workflows]               = useState(MY_WORKFLOWS)
  const [notifs, setNotifs]       = useState(NOTIFS)
  const [showNotifs, setShowNotifs] = useState(false)
  const [showUpload, setShowUpload] = useState(false)
  const [greeting, setGreeting]   = useState('')
  const [newTaskTitle, setNewTaskTitle] = useState('')
  const [showQuickTask, setShowQuickTask] = useState(false)
  const [stats, setStats]         = useState({ totalDocuments:1247, pendingWorkflows:23, activeUsers:48 })
  const notifRef = useRef()

  const unread  = notifs.filter(n => !n.read).length
  const overdue = tasks.filter(t => t.isOverdue).length
  const userName = user?.fullNameAr || user?.username || 'المستخدم'

  useEffect(() => {
    const h = new Date().getHours()
    setGreeting(h < 12 ? 'صباح الخير' : h < 17 ? 'مساء الخير' : 'مساء النور')

    client.get('/api/v1/dashboard/stats')
      .then(r => { if (r.data?.data) setStats(r.data.data) })
      .catch(() => {})

    client.get('/api/v1/workflow/inbox')
      .then(r => {
        const d = r.data?.data?.items || r.data?.data || r.data
        if (Array.isArray(d) && d.length > 0)
          setTasks(d.map(t => ({
            id: t.taskId||t.id, title: t.documentTitleAr||t.title||'مهمة',
            type: t.documentTypeName||'مراجعة',
            due: t.dueAt||t.dueDate,
            priority: t.priority===2?'عاجل':t.priority===1?'مهم':'عادي',
            from: t.assignedFrom||'—',
            isOverdue: t.isOverdue||false,
          })))
      }).catch(() => {})

    // Close notifs on outside click
    const h2 = (e) => { if (!notifRef.current?.contains(e.target)) setShowNotifs(false) }
    document.addEventListener('mousedown', h2)
    return () => document.removeEventListener('mousedown', h2)
  }, [])

  const approveTask = async (id) => {
    try { await client.post(`/api/v1/workflow/tasks/${id}/approve`, { comment:'' }) } catch {}
    setTasks(p => p.filter(t => t.id !== id))
    show('تمت الموافقة بنجاح ✅', 'success')
  }

  const addQuickTask = () => {
    if (!newTaskTitle.trim()) return
    const t = { id: Date.now(), title: newTaskTitle, type:'مهمة', due: null, priority:'عادي', from:'أنا', isOverdue:false }
    setTasks(p => [t, ...p])
    setNewTaskTitle(''); setShowQuickTask(false)
    show('تم إنشاء المهمة بنجاح', 'success')
  }

  const markAllRead = () => setNotifs(p => p.map(n => ({...n, read:true})))

  return (
    <div className="space-y-5 max-w-7xl" dir="rtl">
      <ToastContainer />
      {showUpload && (
        <UploadModal
          onClose={() => setShowUpload(false)}
          onSuccess={({msg, doc}) => {
            show(msg, 'success')
            setShowUpload(false)
            if (doc) setDocs(p => [{ id:doc.id, name:doc.titleAr, icon:'📄', type:doc.fileType||'PDF', modified:'الآن', status:'Draft', size:doc.fileSize||'—' }, ...p])
          }}
        />
      )}

      {/* ══ HERO BANNER ══ */}
      <div className="bg-gradient-to-l from-blue-800 to-blue-600 rounded-2xl p-5 text-white relative overflow-hidden">
        <div className="absolute inset-0 opacity-[0.07]">
          <div className="absolute top-0 right-0 w-72 h-72 bg-white rounded-full translate-x-1/3 -translate-y-1/3"/>
          <div className="absolute bottom-0 left-0 w-48 h-48 bg-white rounded-full -translate-x-1/4 translate-y-1/4"/>
        </div>

        <div className="relative z-10 flex items-start gap-4">
          {/* Avatar */}
          <div className="w-14 h-14 bg-white/20 rounded-2xl flex items-center justify-center text-2xl font-black flex-shrink-0">
            {userName[0] || 'م'}
          </div>

          <div className="flex-1 min-w-0">
            <p className="text-blue-200 text-sm">{greeting}،</p>
            <h1 className="text-xl font-bold mt-0.5 truncate">{userName}</h1>
            <p className="text-blue-300 text-xs mt-0.5">
              {new Date().toLocaleDateString('ar-SA', { weekday:'long', year:'numeric', month:'long', day:'numeric' })}
            </p>

            {/* Status pills */}
            <div className="flex flex-wrap gap-2 mt-3">
              {tasks.length > 0 && (
                <button onClick={() => navigate('/workflows')}
                  className="bg-white/20 hover:bg-white/30 rounded-xl px-3 py-1.5 text-xs font-semibold transition-colors flex items-center gap-1.5">
                  ⏳ {tasks.length} {tasks.length===1?'مهمة':'مهام'} بانتظارك
                </button>
              )}
              {overdue > 0 && (
                <button onClick={() => navigate('/workflows')}
                  className="bg-red-500/30 hover:bg-red-500/40 rounded-xl px-3 py-1.5 text-xs font-semibold transition-colors flex items-center gap-1.5">
                  ⚠️ {overdue} متأخرة
                </button>
              )}
              {unread > 0 && (
                <button onClick={() => setShowNotifs(true)}
                  className="bg-yellow-400/30 hover:bg-yellow-400/40 rounded-xl px-3 py-1.5 text-xs font-semibold transition-colors flex items-center gap-1.5">
                  🔔 {unread} إشعار جديد
                </button>
              )}
            </div>
          </div>

          {/* Notification bell */}
          <div className="relative flex-shrink-0" ref={notifRef}>
            <button onClick={() => setShowNotifs(p => !p)}
              className="w-10 h-10 bg-white/15 hover:bg-white/25 rounded-xl flex items-center justify-center relative transition-colors">
              <span className="text-lg">🔔</span>
              {unread > 0 && (
                <span className="absolute -top-1 -right-1 w-5 h-5 bg-red-500 rounded-full text-[10px] font-bold flex items-center justify-center">
                  {unread}
                </span>
              )}
            </button>

            {showNotifs && (
              <div className="absolute left-0 top-12 w-80 bg-white rounded-2xl shadow-2xl border border-gray-100 z-50 overflow-hidden">
                <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50">
                  <span className="font-bold text-gray-800 text-sm">الإشعارات</span>
                  {unread > 0 && <button onClick={markAllRead} className="text-xs text-blue-600 hover:underline">قراءة الكل</button>}
                </div>
                <div className="max-h-72 overflow-y-auto">
                  {notifs.map(n => (
                    <div key={n.id}
                      onClick={() => setNotifs(p => p.map(x => x.id===n.id ? {...x,read:true} : x))}
                      className={`flex gap-3 px-4 py-3 border-b border-gray-50 cursor-pointer hover:bg-gray-50 ${!n.read?'bg-blue-50/40':''}`}>
                      <span className="text-lg flex-shrink-0 mt-0.5">{NOTIF_ICON[n.type]}</span>
                      <div className="flex-1 min-w-0">
                        <p className={`text-xs leading-relaxed ${!n.read?'font-semibold text-gray-800':'text-gray-600'}`}>{n.text}</p>
                        <p className="text-[10px] text-gray-400 mt-0.5">{n.time}</p>
                      </div>
                      {!n.read && <span className="w-2 h-2 bg-blue-500 rounded-full mt-1.5 flex-shrink-0"/>}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ══ PERSONAL STATS ══ */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[
          { label:'وثائقي',       value: docs.length,                                          icon:'📄', color:'bg-blue-50 text-blue-700 border border-blue-100',     link:'/documents' },
          { label:'مهامي المعلقة',value: tasks.length,                                         icon:'📋', color:'bg-orange-50 text-orange-700 border border-orange-100', link:'/workflows' },
          { label:'سير أعمالي',   value: workflows.filter(w=>w.status==='Active').length,      icon:'🔄', color:'bg-purple-50 text-purple-700 border border-purple-100', link:'/workflows' },
          { label:'المتأخرة',     value: overdue,                                              icon:'⚠️', color:'bg-red-50 text-red-700 border border-red-100',          link:'/workflows' },
        ].map(s => (
          <button key={s.label} onClick={() => navigate(s.link)}
            className={`${s.color} rounded-2xl p-4 text-center hover:shadow-md transition-all hover:-translate-y-0.5 cursor-pointer`}>
            <span className="text-3xl">{s.icon}</span>
            <p className="text-3xl font-black mt-1">{s.value}</p>
            <p className="text-xs font-medium mt-0.5 opacity-80">{s.label}</p>
          </button>
        ))}
      </div>

      {/* ══ QUICK ACTIONS ══ */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {[
          { icon:'📤', label:'رفع وثيقة',    color:'from-blue-600 to-blue-700',   fn:()=>setShowUpload(true) },
          { icon:'🚀', label:'بدء سير عمل',  color:'from-purple-600 to-purple-700',fn:()=>navigate('/workflows') },
          { icon:'✏️', label:'مهمة سريعة',   color:'from-orange-500 to-orange-600',fn:()=>setShowQuickTask(true) },
          { icon:'🔍', label:'بحث في الملفات',color:'from-teal-600 to-teal-700',   fn:()=>navigate('/library') },
        ].map(a => (
          <button key={a.label} onClick={a.fn}
            className={`bg-gradient-to-br ${a.color} text-white rounded-2xl p-4 flex items-center gap-3 hover:opacity-90 hover:shadow-lg transition-all hover:-translate-y-0.5`}>
            <span className="text-2xl">{a.icon}</span>
            <span className="text-sm font-bold">{a.label}</span>
          </button>
        ))}
      </div>

      {/* Quick task input */}
      {showQuickTask && (
        <div className="bg-white border-2 border-orange-200 rounded-2xl p-4 flex gap-3 items-center shadow-sm">
          <span className="text-xl">✏️</span>
          <input
            value={newTaskTitle}
            onChange={e => setNewTaskTitle(e.target.value)}
            onKeyDown={e => e.key==='Enter' && addQuickTask()}
            autoFocus
            placeholder="اكتب عنوان المهمة الجديدة..."
            className="flex-1 text-sm border-none outline-none text-right bg-transparent"
          />
          <button onClick={addQuickTask} className="bg-orange-500 text-white px-4 py-2 rounded-xl text-sm font-semibold hover:bg-orange-600">إضافة</button>
          <button onClick={() => setShowQuickTask(false)} className="text-gray-400 hover:text-gray-600">✕</button>
        </div>
      )}

      {/* ══ MAIN GRID ══ */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">

        {/* ── MY TASKS (left, 2/3) ── */}
        <div className="lg:col-span-2 bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="flex items-center justify-between px-5 py-3.5 border-b border-gray-100 bg-gray-50/50">
            <div className="flex items-center gap-2">
              <span className="text-lg">📋</span>
              <h2 className="font-bold text-gray-800">مهامي</h2>
              {tasks.length > 0 && (
                <span className="bg-blue-600 text-white text-[11px] px-2 py-0.5 rounded-full font-bold">{tasks.length}</span>
              )}
            </div>
            <div className="flex gap-2">
              <button onClick={() => setShowQuickTask(true)}
                className="text-xs bg-orange-50 text-orange-600 hover:bg-orange-100 border border-orange-200 px-2.5 py-1.5 rounded-lg transition-colors">
                + مهمة
              </button>
              <button onClick={() => navigate('/workflows')} className="text-xs text-blue-600 hover:underline font-medium">
                عرض الكل ←
              </button>
            </div>
          </div>

          {tasks.length === 0 ? (
            <div className="py-16 text-center">
              <div className="text-5xl mb-3">🎉</div>
              <p className="font-bold text-gray-600">لا توجد مهام معلقة</p>
              <p className="text-gray-400 text-sm mt-1">أنجزت جميع مهامك!</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-50">
              {tasks.map(t => (
                <div key={t.id} className="flex items-center gap-3 px-5 py-3.5 hover:bg-gray-50 transition-colors group">
                  {/* Priority bar */}
                  <div className={`w-1 h-10 rounded-full flex-shrink-0 ${PRIO_BAR[t.priority]}`}/>

                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-800 truncate">{t.title}</p>
                    <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                      <span className="text-[11px] text-gray-400">{t.type}</span>
                      <span className="text-gray-200">•</span>
                      <span className="text-[11px] text-gray-400">من: {t.from}</span>
                      {t.due && (
                        <>
                          <span className="text-gray-200">•</span>
                          <span className={`text-[11px] font-medium ${t.isOverdue?'text-red-500':'text-gray-400'}`}>
                            {t.isOverdue && '⚠️ '}
                            {new Date(t.due).toLocaleDateString('ar-SA')}
                          </span>
                        </>
                      )}
                    </div>
                  </div>

                  <div className="flex items-center gap-1.5 flex-shrink-0">
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${PRIO_CLS[t.priority]}`}>{t.priority}</span>
                    <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                      <button onClick={() => approveTask(t.id)}
                        className="bg-green-600 text-white text-[11px] px-2.5 py-1.5 rounded-lg hover:bg-green-700 transition-colors">
                        ✓ موافقة
                      </button>
                      <button onClick={() => navigate('/workflows')}
                        className="border border-gray-200 text-gray-500 text-[11px] px-2.5 py-1.5 rounded-lg hover:bg-gray-50 transition-colors">
                        عرض
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* ── RIGHT COLUMN ── */}
        <div className="space-y-4">

          {/* My Favorites */}
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
            <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50/50">
              <div className="flex items-center gap-2">
                <span>⭐</span>
                <h3 className="font-bold text-gray-800 text-sm">مفضلتي</h3>
              </div>
              <button onClick={() => navigate('/library')} className="text-xs text-blue-600 hover:underline">الكل ←</button>
            </div>
            <div className="divide-y divide-gray-50">
              {MY_FAVORITES.map(f => (
                <div key={f.id} onClick={() => navigate('/library')}
                  className="flex items-center gap-3 px-4 py-2.5 hover:bg-gray-50 cursor-pointer group transition-colors">
                  <span className="text-xl flex-shrink-0">{f.icon}</span>
                  <div className="flex-1 min-w-0">
                    <p className="text-xs font-medium text-gray-700 truncate">{f.name}</p>
                    <p className="text-[10px] text-gray-400">{f.category}</p>
                  </div>
                  <span className="text-gray-300 group-hover:text-blue-500 text-sm transition-colors">→</span>
                </div>
              ))}
            </div>
          </div>

          {/* My Workflows */}
          <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
            <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50/50">
              <div className="flex items-center gap-2"><span>🔄</span><h3 className="font-bold text-gray-800 text-sm">سير أعمالي</h3></div>
              <button onClick={() => navigate('/workflows')} className="text-xs text-blue-600 hover:underline">الكل ←</button>
            </div>
            <div className="divide-y divide-gray-50">
              {workflows.map(w => (
                <div key={w.id} onClick={() => navigate('/workflows')}
                  className="px-4 py-3 hover:bg-gray-50 cursor-pointer transition-colors">
                  <div className="flex items-center justify-between mb-1.5">
                    <p className="text-xs font-semibold text-gray-800 truncate flex-1">{w.title}</p>
                    <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-medium flex-shrink-0 mr-2 ${STATUS_CLS[w.status]}`}>
                      {STATUS_LBL[w.status]}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                      <div className={`h-full rounded-full ${w.status==='Completed'?'bg-green-500':'bg-blue-500'}`}
                        style={{width:`${w.progress}%`}}/>
                    </div>
                    <span className="text-[10px] text-gray-400 flex-shrink-0">{w.done}/{w.steps}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* ══ BOTTOM GRID ══ */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">

        {/* MY DOCS */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="flex items-center justify-between px-5 py-3.5 border-b border-gray-100 bg-gray-50/50">
            <div className="flex items-center gap-2"><span>📁</span><h2 className="font-bold text-gray-800">وثائقي الأخيرة</h2></div>
            <div className="flex gap-2">
              <button onClick={() => setShowUpload(true)}
                className="text-xs bg-blue-50 text-blue-600 hover:bg-blue-100 border border-blue-200 px-2.5 py-1.5 rounded-lg transition-colors">
                + رفع
              </button>
              <button onClick={() => navigate('/documents')} className="text-xs text-blue-600 hover:underline font-medium">الكل ←</button>
            </div>
          </div>
          <div className="divide-y divide-gray-50">
            {docs.map(d => (
              <div key={d.id} onClick={() => navigate('/documents')}
                className="flex items-center gap-3 px-5 py-3 hover:bg-gray-50 cursor-pointer transition-colors group">
                <span className="text-xl flex-shrink-0">{d.icon}</span>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-800 truncate">{d.name}</p>
                  <div className="flex items-center gap-2 mt-0.5">
                    <span className="text-[11px] text-gray-400">{d.modified}</span>
                    <span className="text-gray-200">•</span>
                    <span className="text-[11px] text-gray-400">{d.size}</span>
                  </div>
                </div>
                <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium flex-shrink-0 ${STATUS_CLS[d.status]}`}>
                  {STATUS_LBL[d.status]}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* MY ACTIVITY */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="flex items-center gap-2 px-5 py-3.5 border-b border-gray-100 bg-gray-50/50">
            <span>🕐</span>
            <h2 className="font-bold text-gray-800">نشاطي الأخير</h2>
          </div>
          <div className="p-4 space-y-3">
            {MY_ACTIVITY.map((a, i) => (
              <div key={i} className="flex items-center gap-3">
                <div className="w-8 h-8 bg-gray-50 border border-gray-100 rounded-xl flex items-center justify-center text-sm flex-shrink-0">
                  {a.icon}
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-xs text-gray-700">
                    <span className="font-semibold">{a.action}</span>
                    <span className="text-gray-400"> — </span>
                    <span className="text-gray-600 truncate">{a.doc}</span>
                  </p>
                  <p className="text-[10px] text-gray-400 mt-0.5">{a.time}</p>
                </div>
                {i < MY_ACTIVITY.length - 1 && (
                  <div className="absolute right-[2.75rem] h-full w-px bg-gray-100" style={{display:'none'}}/>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
