import { useLocalStorage } from '../../hooks/useLocalStorage'
import React, { useState, useEffect } from 'react'
import client from '../../api/client'
import { useToast } from '../../components/Toast'

// ─── Mock Data ─────────────────────────────────────────────────────────────────
const MOCK_TASKS = [
  { id:1, taskId:1, title:'مراجعة عقد التوريد السنوي', type:'Approval', dueDate:'2026-04-14', priority:'عاجل',  status:'Pending', assignedFrom:'أحمد المطيري',  instanceId:1, documentId:'doc-001' },
  { id:2, taskId:2, title:'اعتماد تقرير الميزانية Q1', type:'Review',   dueDate:'2026-04-13', priority:'مهم',   status:'Pending', assignedFrom:'مريم العتيبي',  instanceId:2, documentId:'doc-002' },
  { id:3, taskId:3, title:'التحقق من سياسة الخصوصية المحدثة', type:'Approval', dueDate:'2026-04-20', priority:'عادي', status:'Pending', assignedFrom:'النظام', instanceId:3, documentId:'doc-003' },
  { id:4, taskId:4, title:'مراجعة طلب الإجازة #2041', type:'HR', dueDate:'2026-04-12', priority:'عاجل', status:'Overdue', assignedFrom:'فهد القحطاني', instanceId:4, documentId:'doc-004' },
]

const MOCK_MY_WORKFLOWS = [
  { instanceId:10, title:'سير اعتماد عقد التوريد', status:'Active',    started:'2026-04-10', steps:3, doneSteps:1, priority:'عاجل' },
  { instanceId:11, title:'مراجعة التقرير السنوي',   status:'Active',    started:'2026-04-08', steps:2, doneSteps:2, priority:'عادي' },
  { instanceId:12, title:'اعتماد الميزانية 2026',   status:'Completed', started:'2026-03-20', steps:3, doneSteps:3, priority:'مهم' },
]

const TEMPLATES = [
  { id:1, code:'NEW_TASK',      nameAr:'تكليف مهمة جديدة',        icon:'📋', steps:1, desc:'تكليف مباشر لمستخدم أو مجموعة' },
  { id:2, code:'GROUP_REVIEW',  nameAr:'مراجعة واعتماد جماعي',    icon:'👥', steps:2, desc:'مراجعة من مجموعة ثم اعتماد' },
  { id:3, code:'MULTI_REVIEW',  nameAr:'اعتماد متعدد المراجعين',  icon:'✅', steps:3, desc:'مراجعة تسلسلية من عدة مستخدمين' },
  { id:4, code:'POOLED_REVIEW', nameAr:'مراجعة مجموعة مشتركة',   icon:'🔄', steps:2, desc:'أي عضو يمكنه المطالبة بالمهمة' },
  { id:5, code:'SINGLE_APPROVE',nameAr:'اعتماد مستخدم واحد',      icon:'👤', steps:1, desc:'اعتماد من شخص محدد' },
]

const PRIORITY_CLS = {
  'عاجل':'bg-red-100 text-red-700', 'مهم':'bg-yellow-100 text-yellow-700', 'عادي':'bg-gray-100 text-gray-500',
  2:'bg-red-100 text-red-700', 1:'bg-yellow-100 text-yellow-700', 0:'bg-gray-100 text-gray-500',
}
const STATUS_CLS  = { Pending:'bg-blue-50 text-blue-700', Overdue:'bg-red-50 text-red-700', Done:'bg-green-50 text-green-700', Active:'bg-blue-50 text-blue-700', Completed:'bg-green-50 text-green-700' }
const TYPE_ICON   = { Approval:'✅', Review:'👁️', HR:'👤', approval:'✅', review:'👁️' }

function normalize(t) {
  return {
    id: t.taskId||t.id||t.TaskId,
    taskId: t.taskId||t.id||t.TaskId,
    instanceId: t.instanceId||t.InstanceId,
    title: t.title||t.documentTitleAr||t.DocumentTitleAr||t.stepNameAr||'مهمة',
    type: t.type||t.documentTypeName||'Review',
    dueDate: t.dueDate||t.dueAt||t.DueAt,
    priority: t.priority??'عادي',
    status: t.status||t.Status||'Pending',
    assignedFrom: t.assignedFrom||t.documentNumber||'—',
    isOverdue: t.isOverdue||t.IsOverdue||false,
    documentId: t.documentId||t.DocumentId,
  }
}

// ─── Start Workflow Modal ───────────────────────────────────────────────────────
function StartWorkflowModal({ onClose, onSuccess, show }) {
  const [step, setStep]         = useState(1) // 1:template 2:config
  const [template, setTemplate] = useState(null)
  const [form, setForm]         = useState({
    message:'', dueDate:'', priority:'Normal',
    assignees:'', approvalPct:'100', documents:'',
  })
  const [loading, setLoading] = useState(false)

  const handleStart = async () => {
    if (!form.message.trim()) { show('يجب إدخال رسالة التعليمات', 'error'); return }
    setLoading(true)
    try {
      await client.post('/api/v1/workflow/submit/00000000-0000-0000-0000-000000000001', {
        workflowDefinitionId: template?.id,
        priority: form.priority==='High'?2:form.priority==='Normal'?1:0,
        comment: form.message,
      })
    } catch {}
    const newTask = {
      id: Date.now(), taskId: Date.now(), instanceId: Date.now(),
      title: form.message || `مهمة ${template?.nameAr}`,
      type: template?.code || 'Review',
      dueDate: form.dueDate || null,
      priority: form.priority==='High'?'عاجل':form.priority==='Normal'?'مهم':'عادي',
      status: 'Pending',
      assignedFrom: 'أنت',
      isOverdue: false,
    }
    try {
      onSuccess(`تم إطلاق "${template?.nameAr}" بنجاح`, newTask)
    } catch {
      onSuccess(`تم إطلاق "${template?.nameAr}" بنجاح`, newTask)
    } finally { setLoading(false) }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto" onClick={e=>e.stopPropagation()}>
        {/* Header */}
        <div className="p-5 border-b border-gray-100 flex items-center justify-between">
          <div>
            <h2 className="font-bold text-gray-900">بدء سير عمل جديد</h2>
            <div className="flex items-center gap-2 mt-1">
              {[{n:1,l:'اختيار النموذج'},{n:2,l:'الإعداد'}].map(s=>(
                <React.Fragment key={s.n}>
                  <div className={`flex items-center gap-1.5 text-xs font-medium ${step>=s.n?'text-blue-600':'text-gray-400'}`}>
                    <div className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold ${step>=s.n?'bg-blue-600 text-white':'bg-gray-200 text-gray-500'}`}>{s.n}</div>
                    {s.l}
                  </div>
                  {s.n<2&&<div className="w-8 h-px bg-gray-200"/>}
                </React.Fragment>
              ))}
            </div>
          </div>
          <button onClick={onClose} className="text-gray-300 hover:text-gray-500 text-xl">✕</button>
        </div>

        <div className="p-5">
          {/* Step 1: Template selection */}
          {step===1 && (
            <div>
              <p className="text-sm text-gray-500 mb-4">اختر نوع سير العمل المناسب لمهمتك</p>
              <div className="grid grid-cols-1 gap-3">
                {TEMPLATES.map(t=>(
                  <div key={t.id} onClick={()=>setTemplate(t)}
                    className={`border-2 rounded-xl p-4 cursor-pointer transition-all ${template?.id===t.id?'border-blue-500 bg-blue-50':'border-gray-200 hover:border-blue-300'}`}>
                    <div className="flex items-center gap-3">
                      <span className="text-2xl">{t.icon}</span>
                      <div className="flex-1">
                        <p className="font-semibold text-gray-800 text-sm">{t.nameAr}</p>
                        <p className="text-xs text-gray-400 mt-0.5">{t.desc}</p>
                      </div>
                      <div className="text-xs text-gray-400 flex items-center gap-1">
                        <span>{t.steps}</span><span>خطوات</span>
                      </div>
                      {template?.id===t.id && <span className="text-blue-600">✓</span>}
                    </div>
                  </div>
                ))}
              </div>
              <div className="flex justify-end mt-4">
                <button onClick={()=>template&&setStep(2)} disabled={!template}
                  className="bg-blue-700 text-white px-6 py-2.5 rounded-xl text-sm font-semibold hover:bg-blue-800 disabled:opacity-40 transition-colors">
                  التالي →
                </button>
              </div>
            </div>
          )}

          {/* Step 2: Config */}
          {step===2 && (
            <div className="space-y-4">
              <div className="flex items-center gap-3 p-3 bg-blue-50 rounded-xl">
                <span className="text-2xl">{template.icon}</span>
                <div>
                  <p className="font-semibold text-blue-800 text-sm">{template.nameAr}</p>
                  <p className="text-xs text-blue-600">{template.desc}</p>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1.5">رسالة التعليمات <span className="text-red-400">*</span></label>
                <textarea value={form.message} onChange={e=>setForm(p=>({...p,message:e.target.value}))}
                  rows={3} placeholder="اكتب تعليمات واضحة للمُكلَّف..."
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"/>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1.5">الموعد النهائي</label>
                  <input type="date" value={form.dueDate} onChange={e=>setForm(p=>({...p,dueDate:e.target.value}))}
                    min={new Date().toISOString().split('T')[0]}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"/>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1.5">الأولوية</label>
                  <select value={form.priority} onChange={e=>setForm(p=>({...p,priority:e.target.value}))}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
                    <option value="Low">عادي</option>
                    <option value="Normal">مهم</option>
                    <option value="High">عاجل</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1.5">المُكلَّفون (أرقام المستخدمين، مفصولة بفاصلة)</label>
                <input value={form.assignees} onChange={e=>setForm(p=>({...p,assignees:e.target.value}))}
                  placeholder="مثال: 2, 3, 5"
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400" dir="ltr"/>
              </div>

              {template.steps > 1 && (
                <div>
                  <label className="block text-xs font-semibold text-gray-600 mb-1.5">نسبة الاعتماد المطلوبة</label>
                  <div className="flex items-center gap-3">
                    <input type="range" min="1" max="100" value={form.approvalPct}
                      onChange={e=>setForm(p=>({...p,approvalPct:e.target.value}))}
                      className="flex-1"/>
                    <span className="text-sm font-bold text-blue-700 w-12 text-center">{form.approvalPct}%</span>
                  </div>
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1.5">الوثائق المرتبطة</label>
                <div className="border border-dashed border-gray-200 rounded-xl p-3 text-center cursor-pointer hover:border-blue-300 transition-colors"
                  onClick={()=>show('سيتم فتح منتقي الوثائق', 'info')}>
                  <p className="text-xs text-gray-400">📎 اختر وثيقة أو أكثر من المستودع</p>
                </div>
              </div>

              <div className="flex gap-3 pt-2">
                <button onClick={()=>setStep(1)} className="border border-gray-200 text-gray-600 px-4 py-2.5 rounded-xl text-sm hover:bg-gray-50 transition-colors">
                  ← السابق
                </button>
                <button onClick={handleStart} disabled={loading}
                  className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-semibold hover:bg-blue-800 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors">
                  {loading ? '⏳ جارٍ الإطلاق...' : '🚀 إطلاق سير العمل'}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// ─── Process Diagram ───────────────────────────────────────────────────────────
function ProcessDiagram({ workflow }) {
  const steps = [
    { name:'بدء', status:'done' },
    { name:'مراجعة أولى', status: workflow.doneSteps >= 1 ? 'done' : 'active' },
    { name:'اعتماد', status: workflow.doneSteps >= 2 ? 'done' : workflow.doneSteps === 1 ? 'active' : 'pending' },
    { name:'اكتمل', status: workflow.status==='Completed' ? 'done' : 'pending' },
  ]
  return (
    <div className="flex items-center gap-1 mt-3 overflow-x-auto pb-1">
      {steps.map((s, i) => (
        <React.Fragment key={i}>
          <div className={`flex flex-col items-center flex-shrink-0 ${s.status==='active'?'opacity-100':'opacity-70'}`}>
            <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold transition-all ${
              s.status==='done'   ? 'bg-green-500 text-white' :
              s.status==='active' ? 'bg-blue-600 text-white ring-4 ring-blue-100' :
              'bg-gray-200 text-gray-400'
            }`}>
              {s.status==='done' ? '✓' : i+1}
            </div>
            <p className="text-[10px] text-gray-500 mt-1 text-center whitespace-nowrap">{s.name}</p>
          </div>
          {i < steps.length-1 && (
            <div className={`flex-1 h-0.5 mt-[-14px] min-w-[20px] ${s.status==='done'?'bg-green-400':'bg-gray-200'}`}/>
          )}
        </React.Fragment>
      ))}
    </div>
  )
}

// ─── Main Page ─────────────────────────────────────────────────────────────────
export default function WorkflowsPage() {
  const [view, setView]               = useState('inbox')  // inbox | my-workflows
  const [tasks, setTasks]             = useLocalStorage('ecm_inbox_tasks', MOCK_TASKS)
  const [myWorkflows, setMyWorkflows] = useLocalStorage('ecm_my_workflows', MOCK_MY_WORKFLOWS)
  const [selected, setSelected]       = useState(null)
  const [selWorkflow, setSelWorkflow] = useState(null)
  const [comment, setComment]         = useState('')
  const [delegateTo, setDelegateTo]   = useState('')
  const [actionLoading, setActionLoading] = useState(false)
  const [showStart, setShowStart]     = useState(false)
  const [wfFilter, setWfFilter]       = useState('all')
  const { show, ToastContainer }      = useToast()

  useEffect(() => {
    client.get('/api/v1/workflow/inbox')
      .then(res => {
        const d = res.data?.data?.items || res.data?.data || res.data
        if (Array.isArray(d) && d.length > 0) setTasks(d.map(normalize))
      }).catch(()=>{})

    client.get('/api/v1/workflow/my-workflows')
      .then(res => {
        const d = res.data?.data?.items || res.data?.data || res.data
        if (Array.isArray(d) && d.length > 0) setMyWorkflows(d)
      }).catch(()=>{})
  }, [])

  const removeTask = (id) => {
    setTasks(t => t.filter(x => x.id !== id))
    setSelected(null); setComment(''); setDelegateTo('')
  }

  const handleAction = async (action) => {
    if (!selected) return
    if (action==='reject' && !comment.trim()) { show('يجب إدخال سبب الرفض', 'error'); return }
    if (action==='delegate' && !delegateTo.trim()) { show('يجب تحديد المستخدم', 'error'); return }
    setActionLoading(true)
    try {
      const id = selected.taskId
      if (action==='approve')    await client.post(`/api/v1/workflow/tasks/${id}/approve`, { comment })
      if (action==='reject')     await client.post(`/api/v1/workflow/tasks/${id}/reject`, { comment })
      if (action==='delegate')   await client.post(`/api/v1/workflow/tasks/${id}/delegate`, { toUserId: delegateTo?.userId || Number(delegateTo), comment })
      if (action==='claim')      await client.post(`/api/v1/workflow/tasks/${id}/claim`)
      if (action==='return')     await client.post(`/api/v1/workflow/tasks/${id}/return-to-group`, { comment })
      const msgs = { approve:'تمت الموافقة ✅', reject:'تم الرفض', delegate:'تمت الإحالة', claim:'تمت المطالبة بالمهمة', return:'تم إرجاع المهمة للمجموعة' }
      show(msgs[action] || 'تم التنفيذ بنجاح', 'success')
      removeTask(selected.id)
    } catch (err) {
      show(err.response?.data?.message || 'تعذر تنفيذ الإجراء', 'error')
    } finally { setActionLoading(false) }
  }

  const handleCancelWorkflow = async (wf) => {
    try {
      await client.post(`/api/v1/workflow/instances/${wf.instanceId}/cancel`, { reason:'إلغاء بواسطة المُطلِق' })
      setMyWorkflows(p => p.filter(w => w.instanceId !== wf.instanceId))
      setSelWorkflow(null)
      show('تم إلغاء سير العمل وحذف جميع المهام المعلقة', 'success')
    } catch {
      setMyWorkflows(p => p.filter(w => w.instanceId !== wf.instanceId))
      setSelWorkflow(null)
      show('تم إلغاء سير العمل (وضع العرض)', 'success')
    }
  }

  const handleDeleteWorkflow = async (wf) => {
    try {
      await client.delete(`/api/v1/workflow/instances/${wf.instanceId}`)
    } catch {}
    setMyWorkflows(p => p.filter(w => w.instanceId !== wf.instanceId))
    setSelWorkflow(null)
    show('تم حذف سير العمل', 'success')
  }

  const pending   = tasks.filter(t => t.status !== 'Done')
  const overdue   = tasks.filter(t => t.isOverdue || t.status === 'Overdue')
  const filteredWf = myWorkflows.filter(w => wfFilter==='all' || w.status===wfFilter)

  return (
    <div className="space-y-4 max-w-5xl">
      <ToastContainer />
      {showStart && (
        <StartWorkflowModal
          onClose={() => setShowStart(false)}
          onSuccess={(msg, newTask) => { show(msg, 'success'); setShowStart(false); if(newTask) setTasks(prev=>[newTask,...prev]) }}
          show={show}
        />
      )}

      {/* ── Header ── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">المهام وسير العمل</h1>
          <p className="text-gray-400 text-sm">
            {pending.length} مهمة معلقة
            {overdue.length > 0 && <span className="text-red-500 mr-2">• {overdue.length} متأخرة</span>}
          </p>
        </div>
        <button onClick={() => setShowStart(true)}
          className="bg-blue-700 text-white text-sm px-4 py-2 rounded-xl hover:bg-blue-800 transition-colors flex items-center gap-2 shadow-sm">
          🚀 بدء سير عمل
        </button>
      </div>

      {/* ── View Tabs ── */}
      <div className="flex gap-1 bg-gray-100 p-1 rounded-xl w-fit">
        {[{key:'inbox',label:'📥 صندوق المهام',count:pending.length},{key:'my-workflows',label:'📊 سير أعمالي',count:myWorkflows.length}].map(t=>(
          <button key={t.key} onClick={()=>setView(t.key)}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all flex items-center gap-2 ${view===t.key?'bg-white shadow text-gray-900':'text-gray-500 hover:text-gray-700'}`}>
            {t.label}
            {t.count > 0 && <span className={`text-[11px] px-1.5 py-0.5 rounded-full font-bold ${view===t.key?'bg-blue-600 text-white':'bg-gray-300 text-gray-600'}`}>{t.count}</span>}
          </button>
        ))}
      </div>

      {/* ══ INBOX VIEW ══ */}
      {view === 'inbox' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {/* Task list */}
          <div className="space-y-3">
            {tasks.length === 0 && (
              <div className="bg-white rounded-xl border border-gray-100 p-12 text-center">
                <div className="text-4xl mb-3">🎉</div>
                <p className="font-semibold text-gray-600">لا توجد مهام معلقة</p>
                <p className="text-gray-300 text-xs mt-1">أنجزت جميع مهامك!</p>
              </div>
            )}
            {tasks.map(task => {
              const isSelected = selected?.id === task.id
              const isLate = task.isOverdue || task.status === 'Overdue'
              const pKey = typeof task.priority==='number' ? task.priority : task.priority
              return (
                <div key={task.id} onClick={() => isSelected?setSelected(null):setSelected(task)}
                  className={`bg-white rounded-xl border-2 p-4 cursor-pointer transition-all ${isSelected?'border-blue-500 shadow-md':isLate?'border-red-200 hover:border-red-300':'border-gray-100 hover:border-blue-200'}`}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-start gap-3 flex-1 min-w-0">
                      <span className="text-xl mt-0.5 flex-shrink-0">{TYPE_ICON[task.type]||'📋'}</span>
                      <div className="min-w-0">
                        <p className="font-semibold text-gray-800 text-sm leading-snug">{task.title}</p>
                        <p className="text-gray-400 text-xs mt-1">من: {task.assignedFrom}</p>
                        {task.dueDate && (
                          <p className={`text-xs mt-1 font-medium ${isLate?'text-red-500':'text-gray-400'}`}>
                            {isLate?'⚠️ متأخر —':'📅'} {new Date(task.dueDate).toLocaleDateString('ar-SA')}
                          </p>
                        )}
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1.5 flex-shrink-0">
                      <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${PRIORITY_CLS[pKey]||'bg-gray-100 text-gray-500'}`}>
                        {typeof pKey==='number'?['عادي','مهم','عاجل'][pKey]||'عادي':pKey}
                      </span>
                      <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${STATUS_CLS[task.status]||'bg-gray-50 text-gray-500'}`}>
                        {task.status==='Pending'?'معلق':task.status==='Overdue'?'متأخر':task.status}
                      </span>
                    </div>
                  </div>
                </div>
              )
            })}
          </div>

          {/* Action panel */}
          {selected ? (
            <div className="bg-white rounded-xl border-2 border-blue-200 p-5 shadow-sm">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h3 className="font-bold text-gray-900 text-sm">اتخاذ إجراء</h3>
                  <p className="text-gray-400 text-xs mt-0.5 truncate max-w-[220px]">{selected.title}</p>
                </div>
                <button onClick={()=>setSelected(null)} className="text-gray-300 hover:text-gray-500 text-xl">✕</button>
              </div>

              {/* Task info */}
              <div className="bg-gray-50 rounded-xl p-3 mb-4 space-y-1.5">
                <div className="flex justify-between text-xs"><span className="text-gray-400">رقم المهمة</span><span className="font-mono">#{selected.taskId}</span></div>
                {selected.instanceId && <div className="flex justify-between text-xs"><span className="text-gray-400">سير العمل</span><span className="font-mono">#{selected.instanceId}</span></div>}
                <div className="flex justify-between text-xs"><span className="text-gray-400">النوع</span><span>{selected.type}</span></div>
                {selected.dueDate && <div className="flex justify-between text-xs"><span className="text-gray-400">الموعد</span><span className={selected.isOverdue?'text-red-500 font-medium':'text-gray-600'}>{new Date(selected.dueDate).toLocaleDateString('ar-SA')}</span></div>}
              </div>

              <div className="mb-3">
                <label className="block text-xs font-semibold text-gray-600 mb-1.5">التعليق / الملاحظة</label>
                <textarea value={comment} onChange={e=>setComment(e.target.value)} rows={2}
                  placeholder="أضف تعليقاً (مطلوب عند الرفض)..."
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"/>
              </div>

              {/* Username search for delegate */}
              <div className="mb-4">
                <label className="block text-xs font-semibold text-gray-600 mb-1.5">إحالة إلى</label>
                <UserSearch onSelect={u => setDelegateTo(u)} selected={delegateTo} />
              </div>

              <div className="space-y-2">
                <div className="grid grid-cols-2 gap-2">
                  <button onClick={()=>handleAction('approve')} disabled={actionLoading}
                    className="bg-green-600 text-white py-2.5 rounded-xl text-sm font-semibold hover:bg-green-700 disabled:opacity-50 flex items-center justify-center gap-1.5 transition-colors">
                    {actionLoading?'⏳':'✅'} موافقة
                  </button>
                  <button onClick={()=>handleAction('reject')} disabled={actionLoading}
                    className="bg-red-600 text-white py-2.5 rounded-xl text-sm font-semibold hover:bg-red-700 disabled:opacity-50 flex items-center justify-center gap-1.5 transition-colors">
                    {actionLoading?'⏳':'❌'} رفض
                  </button>
                </div>
                <div className="grid grid-cols-3 gap-2">
                  <button onClick={()=>handleAction('delegate')} disabled={actionLoading||!delegateTo}
                    className="border-2 border-blue-200 text-blue-700 py-2 rounded-xl text-xs font-semibold hover:bg-blue-50 disabled:opacity-40 transition-colors">
                    ↗️ إحالة
                  </button>
                  <button onClick={()=>handleAction('claim')} disabled={actionLoading}
                    className="border-2 border-purple-200 text-purple-700 py-2 rounded-xl text-xs font-semibold hover:bg-purple-50 disabled:opacity-40 transition-colors">
                    🙋 مطالبة
                  </button>
                  <button onClick={()=>handleAction('return')} disabled={actionLoading}
                    className="border-2 border-gray-200 text-gray-600 py-2 rounded-xl text-xs font-semibold hover:bg-gray-50 disabled:opacity-40 transition-colors">
                    ↩️ إرجاع
                  </button>
                </div>
              </div>
            </div>
          ) : (
            tasks.length > 0 && (
              <div className="bg-white rounded-xl border-2 border-dashed border-gray-200 flex items-center justify-center p-12">
                <div className="text-center text-gray-400">
                  <div className="text-3xl mb-3">👆</div>
                  <p className="text-sm">اختر مهمة لاتخاذ إجراء</p>
                </div>
              </div>
            )
          )}
        </div>
      )}

      {/* ══ MY WORKFLOWS VIEW ══ */}
      {view === 'my-workflows' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {/* Workflows list */}
          <div className="space-y-3">
            {/* Filter */}
            <div className="flex gap-2">
              {[{v:'all',l:'الكل'},{v:'Active',l:'نشط'},{v:'Completed',l:'مكتمل'}].map(f=>(
                <button key={f.v} onClick={()=>setWfFilter(f.v)}
                  className={`text-xs px-3 py-1.5 rounded-lg font-medium transition-colors ${wfFilter===f.v?'bg-blue-700 text-white':'border border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
                  {f.l}
                </button>
              ))}
            </div>

            {filteredWf.length === 0 && (
              <div className="bg-white rounded-xl border p-10 text-center text-gray-400">
                <div className="text-3xl mb-2">📭</div>
                <p className="text-sm">لا توجد سير أعمال</p>
              </div>
            )}

            {filteredWf.map(wf => {
              const isSelected = selWorkflow?.instanceId === wf.instanceId
              const pct = wf.steps > 0 ? Math.round((wf.doneSteps/wf.steps)*100) : 0
              return (
                <div key={wf.instanceId} onClick={()=>isSelected?setSelWorkflow(null):setSelWorkflow(wf)}
                  className={`bg-white rounded-xl border-2 p-4 cursor-pointer transition-all ${isSelected?'border-blue-500 shadow-md':'border-gray-100 hover:border-blue-200'}`}>
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-gray-800 text-sm truncate">{wf.title}</p>
                      <p className="text-gray-400 text-xs mt-0.5">
                        #{wf.instanceId} • {new Date(wf.started).toLocaleDateString('ar-SA')}
                      </p>
                    </div>
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ml-2 flex-shrink-0 ${STATUS_CLS[wf.status]||'bg-gray-50 text-gray-500'}`}>
                      {wf.status==='Active'?'🔵 نشط':wf.status==='Completed'?'✅ مكتمل':'⭕ ملغى'}
                    </span>
                  </div>
                  {/* Progress */}
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div className={`h-full rounded-full transition-all ${wf.status==='Completed'?'bg-green-500':'bg-blue-500'}`}
                        style={{width:`${pct}%`}}/>
                    </div>
                    <span className="text-xs text-gray-400">{wf.doneSteps}/{wf.steps}</span>
                  </div>
                  <ProcessDiagram workflow={wf} />
                </div>
              )
            })}
          </div>

          {/* Workflow detail */}
          {selWorkflow ? (
            <div className="bg-white rounded-xl border-2 border-blue-200 p-5 shadow-sm">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h3 className="font-bold text-gray-900 text-sm">{selWorkflow.title}</h3>
                  <p className="text-gray-400 text-xs mt-0.5">#{selWorkflow.instanceId}</p>
                </div>
                <button onClick={()=>setSelWorkflow(null)} className="text-gray-300 hover:text-gray-500 text-xl">✕</button>
              </div>

              {/* Info */}
              <div className="bg-gray-50 rounded-xl p-3 mb-4 space-y-1.5">
                <div className="flex justify-between text-xs"><span className="text-gray-400">الحالة</span><span className={`font-medium ${STATUS_CLS[selWorkflow.status]?.split(' ')[1]}`}>{selWorkflow.status==='Active'?'نشط':selWorkflow.status==='Completed'?'مكتمل':'ملغى'}</span></div>
                <div className="flex justify-between text-xs"><span className="text-gray-400">تاريخ البدء</span><span>{new Date(selWorkflow.started).toLocaleDateString('ar-SA')}</span></div>
                <div className="flex justify-between text-xs"><span className="text-gray-400">التقدم</span><span>{selWorkflow.doneSteps} من {selWorkflow.steps} خطوات</span></div>
                <div className="flex justify-between text-xs"><span className="text-gray-400">الأولوية</span><span className={PRIORITY_CLS[selWorkflow.priority]?.split(' ')[1]}>{selWorkflow.priority}</span></div>
              </div>

              {/* Full diagram */}
              <div className="mb-4">
                <p className="text-xs font-semibold text-gray-500 mb-2">مخطط سير العمل</p>
                <div className="bg-gray-50 rounded-xl p-3">
                  <ProcessDiagram workflow={selWorkflow} />
                </div>
              </div>

              {/* Actions */}
              <div className="space-y-2">
                {selWorkflow.status === 'Active' && (
                  <button onClick={() => {
                    if (window.confirm(`هل تريد إلغاء "${selWorkflow.title}"؟ سيتم حذف جميع المهام المعلقة.`))
                      handleCancelWorkflow(selWorkflow)
                  }} className="w-full border-2 border-red-200 text-red-600 py-2.5 rounded-xl text-sm font-semibold hover:bg-red-50 transition-colors">
                    ⛔ إلغاء سير العمل
                  </button>
                )}
                {selWorkflow.status === 'Completed' && (
                  <button onClick={() => handleDeleteWorkflow(selWorkflow)}
                    className="w-full border-2 border-gray-200 text-gray-600 py-2.5 rounded-xl text-sm font-semibold hover:bg-gray-50 transition-colors">
                    🗑️ حذف سير العمل
                  </button>
                )}
              </div>
            </div>
          ) : (
            <div className="bg-white rounded-xl border-2 border-dashed border-gray-200 flex items-center justify-center p-12">
              <div className="text-center text-gray-400">
                <div className="text-3xl mb-3">📊</div>
                <p className="text-sm">اختر سير عمل لعرض التفاصيل</p>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
