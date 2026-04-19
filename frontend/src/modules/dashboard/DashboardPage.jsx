import React, { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import client from '../../api/client'
import { useToast } from '../../components/Toast'
import { PreviewModal } from '../../components/PreviewModal'
import { ShareModal } from '../../components/ShareModal'

// ─── Personal mock data ────────────────────────────────────────────────────────
const MY_TASKS = [
  { id:1, title:'مراجعة عقد التوريد السنوي',    type:'اعتماد', due:'2026-04-14', priority:'عاجل', from:'أحمد المطيري', isOverdue:true  },
  { id:2, title:'اعتماد تقرير الميزانية Q1',    type:'مراجعة', due:'2026-04-20', priority:'مهم',  from:'مريم العتيبي', isOverdue:false },
  { id:3, title:'التحقق من سياسة الخصوصية',   type:'اعتماد', due:'2026-04-25', priority:'عادي', from:'النظام',        isOverdue:false },
  { id:4, title:'مراجعة طلب إجازة #2041',      type:'HR',     due:'2026-04-12', priority:'عاجل', from:'فهد القحطاني', isOverdue:true  },
]

const MY_DOCS = [
  { id:'d1', name:'تقرير الميزانية السنوي 2026.pdf', type:'PDF',  icon:'📕', modified:'منذ ساعة',    status:'Approved',     version:'3.1', size:'2.4 MB', owner:'أحمد الزهراني', classification:'سري',   pages:48 },
  { id:'d2', name:'عقد توريد المستلزمات.docx',       type:'DOCX', icon:'📘', modified:'منذ 3 ساعات', status:'UnderReview',  version:'2.0', size:'1.2 MB', owner:'أحمد الزهراني', classification:'داخلي', pages:22 },
  { id:'d3', name:'سياسة حماية البيانات.pdf',        type:'PDF',  icon:'📕', modified:'أمس',          status:'Active',       version:'4.0', size:'0.9 MB', owner:'أحمد الزهراني', classification:'عام',   pages:35 },
  { id:'d4', name:'خطة الاستمرارية التشغيلية.docx',  type:'DOCX', icon:'📘', modified:'منذ يومين',   status:'Draft',        version:'0.3', size:'1.8 MB', owner:'أحمد الزهراني', classification:'داخلي', pages:60 },
  { id:'d5', name:'عرض الرؤية الاستراتيجية.pptx',   type:'PPTX', icon:'📙', modified:'منذ 3 أيام',  status:'Active',       version:'1.5', size:'8.2 MB', owner:'أحمد الزهراني', classification:'سري',   pages:32 },
]

const MY_WORKFLOWS = [
  { id:10, title:'سير اعتماد عقد التوريد', status:'Active',    progress:33,  steps:3, done:1, started:'2026-04-10', priority:'عاجل' },
  { id:11, title:'مراجعة التقرير السنوي',  status:'Active',    progress:67,  steps:3, done:2, started:'2026-04-08', priority:'مهم'  },
  { id:12, title:'اعتماد الميزانية 2026',  status:'Completed', progress:100, steps:3, done:3, started:'2026-03-20', priority:'عادي' },
]

const MY_FAVORITES = [
  { id:'d1', name:'تقرير الميزانية 2026',      icon:'📕', type:'PDF',  modified:'2026-04-10' },
  { id:'d3', name:'سياسة حماية البيانات',      icon:'📕', type:'PDF',  modified:'2026-03-25' },
  { id:'d8', name:'عرض الرؤية الاستراتيجية',  icon:'📙', type:'PPTX', modified:'2026-04-12' },
]

const MY_SHARED = [
  { id:'s1', name:'تقرير الميزانية السنوي', icon:'📕', sharedBy:'مريم العنزي',   permission:'قراءة',  time:'منذ ساعة'    },
  { id:'s2', name:'لائحة الإجراءات',        icon:'📘', sharedBy:'خالد القحطاني', permission:'تعليق', time:'أمس'          },
  { id:'s3', name:'خطة المشروع Q2',         icon:'📗', sharedBy:'نورة السبيعي',  permission:'تحرير', time:'منذ يومين'    },
]

const ACTIVITY = [
  { id:1, type:'approve', text:'اعتمدت وثيقة: عقد توريد المستلزمات',     time:'منذ 10 دقائق', icon:'✅' },
  { id:2, type:'upload',  text:'رفعت وثيقة: خطة الاستمرارية التشغيلية', time:'منذ ساعتين',    icon:'📤' },
  { id:3, type:'share',   text:'شاركت وثيقة مع مريم العنزي',            time:'أمس',           icon:'🔗' },
  { id:4, type:'task',    text:'أُسندت إليك مهمة: مراجعة عقد التوريد',  time:'أمس',           icon:'📋' },
  { id:5, type:'comment', text:'علّقت على: سياسة حماية البيانات',       time:'منذ 3 أيام',    icon:'💬' },
]

const NOTIFS = [
  { id:1, text:'طلب موافقة: عقد التوريد السنوي',    time:'منذ 5 دقائق', type:'task',    read:false },
  { id:2, text:'تمت الموافقة على طلبك: تقرير Q1',   time:'منذ ساعة',    type:'approve', read:false },
  { id:3, text:'مشاركة جديدة من خالد القحطاني',      time:'منذ 3 ساعات', type:'share',   read:false },
  { id:4, text:'تذكير: مهمة متأخرة بانتظارك',       time:'منذ يومين',   type:'warning', read:true  },
]

const STATUS_CLS = { Active:'bg-green-100 text-green-700', Approved:'bg-blue-100 text-blue-700', Draft:'bg-purple-100 text-purple-700', Completed:'bg-green-100 text-green-700', UnderReview:'bg-yellow-100 text-yellow-700' }
const STATUS_LBL = { Active:'نشط', Approved:'معتمد', Draft:'مسودة', Completed:'مكتمل', UnderReview:'قيد المراجعة' }
const PRIO_CLS   = { 'عاجل':'bg-red-100 text-red-700', 'مهم':'bg-yellow-100 text-yellow-700', 'عادي':'bg-gray-100 text-gray-500' }
const PERM_CLS   = { 'قراءة':'bg-gray-100 text-gray-600', 'تعليق':'bg-blue-100 text-blue-600', 'تحرير':'bg-yellow-100 text-yellow-700', 'اعتماد':'bg-green-100 text-green-700' }

// ─── Section Header ────────────────────────────────────────────────────────────
function SectionHeader({ icon, title, count, to, navigate, extra }) {
  return (
    <div className="flex items-center justify-between px-5 py-3.5 border-b border-gray-100">
      <div className="flex items-center gap-2">
        <span>{icon}</span>
        <h2 className="font-bold text-gray-800 text-sm">{title}</h2>
        {count !== undefined && count > 0 && (
          <span className="bg-blue-600 text-white text-[10px] px-2 py-0.5 rounded-full font-bold">{count}</span>
        )}
      </div>
      <div className="flex items-center gap-2">
        {extra}
        {to && <button onClick={()=>navigate(to)} className="text-xs text-blue-600 hover:underline font-medium">عرض الكل ←</button>}
      </div>
    </div>
  )
}

// ─── Main Dashboard ────────────────────────────────────────────────────────────
export default function DashboardPage() {
  const { user }    = useAuthStore()
  const navigate    = useNavigate()
  const { show, ToastContainer } = useToast()

  const [tasks, setTasks]           = useState(MY_TASKS)
  const [docs]                      = useState(MY_DOCS)
  const [notifs, setNotifs]         = useState(NOTIFS)
  const [showNotifs, setShowNotifs] = useState(false)
  const [greeting, setGreeting]     = useState('')
  const [activeTab, setActiveTab]   = useState('tasks') // tasks|docs|workflows|favorites|shared|activity
  const [previewDoc, setPreviewDoc] = useState(null)
  const [shareDoc, setShareDoc]     = useState(null)

  const unread    = notifs.filter(n => !n.read).length
  const userName  = user?.fullNameAr || user?.username || 'المستخدم'
  const overdueCt = tasks.filter(t => t.isOverdue).length

  useEffect(() => {
    const h = new Date().getHours()
    setGreeting(h < 12 ? 'صباح الخير' : h < 17 ? 'مساء الخير' : 'مساء النور')

    client.get('/api/v1/workflow/inbox').then(res => {
      const d = res.data?.data?.items || res.data?.data || res.data
      if (Array.isArray(d) && d.length > 0)
        setTasks(d.map(t => ({
          id: t.taskId||t.id, title: t.documentTitleAr||t.title||'مهمة',
          type: t.documentTypeName||'مراجعة',
          due: t.dueAt||t.dueDate,
          priority: t.priority>1?'عاجل':t.priority>0?'مهم':'عادي',
          from: '—', isOverdue: t.isOverdue||false,
        })))
    }).catch(()=>{})
  }, [])

  const approveTask = (id) => {
    client.post(`/api/v1/workflow/tasks/${id}/approve`, { comment:'' }).catch(()=>{})
    setTasks(p => p.filter(t => t.id !== id))
    show('تمت الموافقة بنجاح ✅', 'success')
  }
  const rejectTask = (id) => {
    setTasks(p => p.filter(t => t.id !== id))
    show('تم الرفض', 'success')
  }

  const TABS = [
    { key:'tasks',     label:'مهامي',          icon:'📋', count: tasks.length },
    { key:'docs',      label:'وثائقي',          icon:'📄', count: docs.length },
    { key:'workflows', label:'سير أعمالي',      icon:'🔄', count: MY_WORKFLOWS.filter(w=>w.status==='Active').length },
    { key:'favorites', label:'المفضلة',         icon:'⭐', count: MY_FAVORITES.length },
    { key:'shared',    label:'مُشارَكة معي',    icon:'🤝', count: MY_SHARED.length },
    { key:'activity',  label:'نشاطي',           icon:'⚡', count: null },
  ]

  return (
    <div className="space-y-5 max-w-7xl">
      <ToastContainer />
      {previewDoc && <PreviewModal file={previewDoc} onClose={()=>setPreviewDoc(null)} show={show} />}
      {shareDoc   && <ShareModal   file={shareDoc}   onClose={()=>setShareDoc(null)}   show={show} />}

      {/* ── Personal Header ── */}
      <div className="bg-gradient-to-l from-blue-800 to-blue-950 rounded-2xl p-5 text-white relative overflow-hidden">
        <div className="absolute inset-0 opacity-[0.06]">
          <div className="absolute top-0 left-0 w-72 h-72 bg-white rounded-full -translate-x-1/3 -translate-y-1/3"/>
          <div className="absolute bottom-0 right-16 w-56 h-56 bg-white rounded-full translate-y-1/3"/>
        </div>
        <div className="relative flex items-start justify-between gap-4">
          {/* Left: greeting */}
          <div className="flex items-start gap-4 flex-1 min-w-0">
            <div className="w-12 h-12 bg-white/20 rounded-xl flex items-center justify-center text-2xl font-bold flex-shrink-0">
              {userName[0]}
            </div>
            <div className="min-w-0">
              <p className="text-blue-200 text-sm">{greeting}،</p>
              <h1 className="text-xl font-bold truncate">{userName}</h1>
              <p className="text-blue-300 text-xs mt-1">
                {new Date().toLocaleDateString('ar-SA', { weekday:'long', year:'numeric', month:'long', day:'numeric' })}
              </p>
              <div className="flex flex-wrap gap-2 mt-3">
                {tasks.length > 0 && (
                  <span className="bg-white/15 text-white text-xs px-3 py-1 rounded-full font-medium">
                    ⏳ {tasks.length} مهمة معلقة
                  </span>
                )}
                {overdueCt > 0 && (
                  <span className="bg-red-500/50 text-white text-xs px-3 py-1 rounded-full font-medium">
                    ⚠️ {overdueCt} متأخرة
                  </span>
                )}
                <span className="bg-white/15 text-white text-xs px-3 py-1 rounded-full font-medium">
                  📄 {docs.length} وثيقة
                </span>
                <span className="bg-white/15 text-white text-xs px-3 py-1 rounded-full font-medium">
                  🔄 {MY_WORKFLOWS.filter(w=>w.status==='Active').length} سير عمل نشط
                </span>
              </div>
            </div>
          </div>

          {/* Right: quick actions + bell */}
          <div className="flex flex-col items-end gap-3 flex-shrink-0">
            {/* Notification bell */}
            <div className="relative">
              <button onClick={() => setShowNotifs(p=>!p)}
                className="w-10 h-10 bg-white/15 hover:bg-white/25 rounded-xl flex items-center justify-center transition-colors relative">
                <span className="text-lg">🔔</span>
                {unread > 0 && (
                  <span className="absolute -top-1 -right-1 w-4 h-4 bg-red-500 rounded-full text-[9px] font-bold flex items-center justify-center">
                    {unread}
                  </span>
                )}
              </button>
              {showNotifs && (
                <div className="absolute left-0 top-12 w-80 bg-white rounded-2xl shadow-2xl border border-gray-100 z-50 overflow-hidden" onClick={e=>e.stopPropagation()}>
                  <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
                    <span className="font-bold text-gray-800 text-sm">الإشعارات</span>
                    <button onClick={()=>setNotifs(p=>p.map(n=>({...n,read:true})))} className="text-xs text-blue-600 hover:underline">قراءة الكل</button>
                  </div>
                  {notifs.map(n => (
                    <div key={n.id} onClick={()=>setNotifs(p=>p.map(x=>x.id===n.id?{...x,read:true}:x))}
                      className={`flex gap-3 px-4 py-3 border-b border-gray-50 cursor-pointer hover:bg-gray-50 ${!n.read?'bg-blue-50/60':''}`}>
                      <span className="text-lg flex-shrink-0">{n.type==='task'?'📋':n.type==='approve'?'✅':n.type==='share'?'🔗':'⚠️'}</span>
                      <div className="flex-1 min-w-0">
                        <p className={`text-xs leading-relaxed ${!n.read?'font-semibold text-gray-800':'text-gray-600'}`}>{n.text}</p>
                        <p className="text-[10px] text-gray-400 mt-0.5">{n.time}</p>
                      </div>
                      {!n.read && <span className="w-2 h-2 bg-blue-500 rounded-full mt-1.5 flex-shrink-0"/>}
                    </div>
                  ))}
                  <button onClick={()=>setShowNotifs(false)} className="w-full py-2.5 text-xs text-blue-600 hover:bg-gray-50 font-medium border-t border-gray-100">
                    عرض كل الإشعارات
                  </button>
                </div>
              )}
            </div>

            {/* Quick actions */}
            <div className="flex gap-2">
              {[
                { icon:'📤', label:'رفع',    to:'/documents' },
                { icon:'🚀', label:'سير عمل',to:'/workflows' },
                { icon:'📚', label:'المكتبة',to:'/library'   },
              ].map(a => (
                <button key={a.to} onClick={()=>navigate(a.to)}
                  className="bg-white/15 hover:bg-white/25 transition-colors rounded-xl px-3 py-2 flex flex-col items-center gap-1 min-w-[56px]">
                  <span className="text-lg">{a.icon}</span>
                  <span className="text-[10px] font-medium">{a.label}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* ── Tabs ── */}
      <div className="flex gap-1 overflow-x-auto pb-1">
        {TABS.map(t => (
          <button key={t.key} onClick={() => setActiveTab(t.key)}
            className={`flex items-center gap-1.5 px-4 py-2 rounded-xl text-sm font-medium whitespace-nowrap transition-all flex-shrink-0 ${activeTab===t.key?'bg-blue-700 text-white shadow-sm':'bg-white border border-gray-200 text-gray-600 hover:border-blue-300'}`}>
            <span>{t.icon}</span>
            <span>{t.label}</span>
            {t.count !== null && t.count > 0 && (
              <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-bold ${activeTab===t.key?'bg-white/30 text-white':'bg-gray-100 text-gray-600'}`}>
                {t.count}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* ══ TAB: مهامي ══ */}
      {activeTab === 'tasks' && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <SectionHeader icon="📋" title="مهامي" count={tasks.length} to="/workflows" navigate={navigate} />
          {tasks.length === 0 ? (
            <div className="py-16 text-center">
              <div className="text-5xl mb-3">🎉</div>
              <p className="font-semibold text-gray-600">أنجزت جميع مهامك!</p>
              <p className="text-gray-400 text-xs mt-1">لا توجد مهام معلقة</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-50">
              {tasks.map(t => (
                <div key={t.id} className="flex items-center gap-4 px-5 py-4 hover:bg-gray-50/60 transition-colors group">
                  <div className={`w-1.5 self-stretch rounded-full flex-shrink-0 ${t.isOverdue?'bg-red-400':t.priority==='عاجل'?'bg-orange-400':t.priority==='مهم'?'bg-yellow-400':'bg-gray-200'}`}/>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-800 leading-snug">{t.title}</p>
                    <div className="flex items-center gap-2 mt-1 flex-wrap">
                      <span className="text-[11px] text-gray-400">{t.type}</span>
                      <span className="text-gray-200">•</span>
                      <span className="text-[11px] text-gray-400">من: {t.from}</span>
                      {t.due && (
                        <span className={`text-[11px] font-medium ${t.isOverdue?'text-red-500':'text-gray-400'}`}>
                          • {t.isOverdue?'⚠️ متأخر — ':''}{new Date(t.due).toLocaleDateString('ar-SA')}
                        </span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${PRIO_CLS[t.priority]}`}>{t.priority}</span>
                    <div className="flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                      <button onClick={()=>approveTask(t.id)}
                        className="bg-green-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-green-700 transition-colors font-medium">
                        ✅ موافقة
                      </button>
                      <button onClick={()=>rejectTask(t.id)}
                        className="bg-red-500 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-red-600 transition-colors font-medium">
                        ❌ رفض
                      </button>
                      <button onClick={()=>navigate('/workflows')}
                        className="border border-gray-200 text-gray-600 text-xs px-3 py-1.5 rounded-lg hover:bg-gray-50 transition-colors">
                        عرض
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ══ TAB: وثائقي ══ */}
      {activeTab === 'docs' && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <SectionHeader icon="📄" title="وثائقي الأخيرة" count={docs.length} to="/documents" navigate={navigate} />
          <div className="divide-y divide-gray-50">
            {docs.map(d => (
              <div key={d.id} className="flex items-center gap-4 px-5 py-3.5 hover:bg-gray-50/60 transition-colors group">
                <span className="text-2xl flex-shrink-0">{d.icon}</span>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-800 truncate">{d.name}</p>
                  <div className="flex items-center gap-2 mt-0.5">
                    <span className="text-[11px] text-gray-400">v{d.version}</span>
                    <span className="text-gray-200">•</span>
                    <span className="text-[11px] text-gray-400">{d.size}</span>
                    <span className="text-gray-200">•</span>
                    <span className="text-[11px] text-gray-400">{d.modified}</span>
                  </div>
                </div>
                <div className="flex items-center gap-2 flex-shrink-0">
                  <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium hidden sm:block ${STATUS_CLS[d.status]}`}>{STATUS_LBL[d.status]}</span>
                  <div className="flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button onClick={()=>setPreviewDoc(d)} className="border border-gray-200 text-gray-600 text-xs px-2.5 py-1.5 rounded-lg hover:bg-gray-50">👁 معاينة</button>
                    <button onClick={()=>setShareDoc(d)}   className="border border-gray-200 text-gray-600 text-xs px-2.5 py-1.5 rounded-lg hover:bg-gray-50">🔗 مشاركة</button>
                    <button onClick={()=>navigate('/documents')} className="bg-blue-600 text-white text-xs px-2.5 py-1.5 rounded-lg hover:bg-blue-700">فتح</button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ══ TAB: سير أعمالي ══ */}
      {activeTab === 'workflows' && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <SectionHeader icon="🔄" title="سير أعمالي" to="/workflows" navigate={navigate}
            extra={<span className="text-xs text-gray-400">{MY_WORKFLOWS.filter(w=>w.status==='Active').length} نشط</span>} />
          <div className="divide-y divide-gray-50">
            {MY_WORKFLOWS.map(w => (
              <div key={w.id} onClick={()=>navigate('/workflows')}
                className="px-5 py-4 hover:bg-gray-50/60 cursor-pointer transition-colors group">
                <div className="flex items-center gap-3 mb-2.5">
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-800 truncate">{w.title}</p>
                    <p className="text-[11px] text-gray-400 mt-0.5">
                      بدأ: {new Date(w.started).toLocaleDateString('ar-SA')} • {w.done} من {w.steps} خطوات
                    </p>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${PRIO_CLS[w.priority]}`}>{w.priority}</span>
                    <span className={`text-[10px] px-2.5 py-1 rounded-full font-medium ${STATUS_CLS[w.status]}`}>{STATUS_LBL[w.status]}</span>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <div className="flex-1 h-2.5 bg-gray-100 rounded-full overflow-hidden">
                    <div className={`h-full rounded-full transition-all ${w.status==='Completed'?'bg-green-500':'bg-blue-500'}`}
                      style={{width:`${w.progress}%`}}/>
                  </div>
                  <span className="text-xs font-bold text-gray-500">{w.progress}%</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ══ TAB: المفضلة ══ */}
      {activeTab === 'favorites' && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <SectionHeader icon="⭐" title="المفضلة" count={MY_FAVORITES.length} to="/library" navigate={navigate} />
          <div className="divide-y divide-gray-50">
            {MY_FAVORITES.map(f => (
              <div key={f.id} className="flex items-center gap-4 px-5 py-3.5 hover:bg-gray-50/60 transition-colors group">
                <span className="text-2xl flex-shrink-0">{f.icon}</span>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-800 truncate">{f.name}</p>
                  <p className="text-[11px] text-gray-400">{f.type} • {new Date(f.modified).toLocaleDateString('ar-SA')}</p>
                </div>
                <div className="flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button onClick={()=>setPreviewDoc(f)} className="border border-gray-200 text-gray-600 text-xs px-2.5 py-1.5 rounded-lg hover:bg-gray-50">👁 معاينة</button>
                  <button onClick={()=>navigate('/library')} className="bg-blue-600 text-white text-xs px-2.5 py-1.5 rounded-lg hover:bg-blue-700">فتح</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ══ TAB: مُشارَكة معي ══ */}
      {activeTab === 'shared' && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <SectionHeader icon="🤝" title="ملفات مُشارَكة معي" count={MY_SHARED.length} to="/library" navigate={navigate} />
          <div className="divide-y divide-gray-50">
            {MY_SHARED.map(s => (
              <div key={s.id} className="flex items-center gap-4 px-5 py-3.5 hover:bg-gray-50/60 transition-colors group">
                <span className="text-2xl flex-shrink-0">{s.icon}</span>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-800 truncate">{s.name}</p>
                  <p className="text-[11px] text-gray-400">شاركه: {s.sharedBy} • {s.time}</p>
                </div>
                <div className="flex items-center gap-2 flex-shrink-0">
                  <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${PERM_CLS[s.permission]}`}>{s.permission}</span>
                  <div className="flex gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button onClick={()=>setPreviewDoc({...s, version:'1.0', size:'—', owner:s.sharedBy, classification:'داخلي', pages:10})}
                      className="border border-gray-200 text-gray-600 text-xs px-2.5 py-1.5 rounded-lg hover:bg-gray-50">👁 معاينة</button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ══ TAB: نشاطي ══ */}
      {activeTab === 'activity' && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <SectionHeader icon="⚡" title="سجل نشاطي الأخير" navigate={navigate} />
          <div className="p-5 space-y-1">
            {ACTIVITY.map((a, i) => (
              <div key={a.id} className="flex gap-4">
                <div className="flex flex-col items-center">
                  <div className="w-9 h-9 bg-gray-50 border-2 border-gray-100 rounded-full flex items-center justify-center text-base flex-shrink-0">
                    {a.icon}
                  </div>
                  {i < ACTIVITY.length-1 && <div className="w-px flex-1 bg-gray-100 my-1"/>}
                </div>
                <div className="flex-1 pb-4">
                  <p className="text-sm text-gray-800">{a.text}</p>
                  <p className="text-[11px] text-gray-400 mt-0.5">{a.time}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
