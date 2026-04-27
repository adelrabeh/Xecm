import { useUsers } from '../../hooks/useUsers'
import React, { useState, useEffect } from 'react'
import { useLocalStorage } from '../../hooks/useLocalStorage'
import { WorkflowBuilderModal } from '../../components/WorkflowBuilderModal'
import client from '../../api/client'
import { useToast } from '../../components/Toast'

// ─── Mock Data ─────────────────────────────────────────────────────────────────
const MOCK_TASKS = [
  { id:1, taskId:1, title:'مراجعة عقد التوريد السنوي',      type:'Approval', dueDate:'2026-04-14', priority:'عاجل', status:'Pending', assignedFrom:'أحمد المطيري', instanceId:1 },
  { id:2, taskId:2, title:'اعتماد تقرير الميزانية Q1',      type:'Review',   dueDate:'2026-04-22', priority:'مهم',  status:'Pending', assignedFrom:'مريم العتيبي', instanceId:2 },
  { id:3, taskId:3, title:'التحقق من سياسة الخصوصية',       type:'Approval', dueDate:'2026-04-28', priority:'عادي',status:'Pending', assignedFrom:'النظام',       instanceId:3 },
  { id:4, taskId:4, title:'مراجعة طلب الإجازة #2041',       type:'HR',       dueDate:'2026-04-12', priority:'عاجل',status:'Overdue', assignedFrom:'فهد القحطاني', instanceId:4, isOverdue:true },
]

const MOCK_WORKFLOWS = [
  { instanceId:10, title:'سير اعتماد عقد التوريد', status:'Active',    progress:33,  steps:3, doneSteps:1, started:'2026-04-10', priority:'عاجل' },
  { instanceId:11, title:'مراجعة التقرير السنوي',   status:'Active',    progress:67,  steps:3, doneSteps:2, started:'2026-04-08', priority:'عادي' },
  { instanceId:12, title:'اعتماد الميزانية 2026',   status:'Completed', progress:100, steps:3, doneSteps:3, started:'2026-03-20', priority:'مهم'  },
]

const PRIORITY_CLS = {
  'عاجل':'bg-red-100 text-red-700', 'مهم':'bg-yellow-100 text-yellow-700', 'عادي':'bg-gray-100 text-gray-500',
}
const STATUS_CLS = {
  Active:'bg-blue-50 text-blue-700', Completed:'bg-green-50 text-green-700', Cancelled:'bg-gray-50 text-gray-500',
}

function normalize(t) {
  return {
    id: t.taskId || t.id,
    taskId: t.taskId || t.id,
    instanceId: t.instanceId,
    title: t.title || t.documentTitleAr || 'مهمة',
    type: t.type || t.documentTypeName || 'Review',
    dueDate: t.dueDate || t.dueAt,
    priority: typeof t.priority === 'number' ? ['عادي','مهم','عاجل'][t.priority] || 'عادي' : (t.priority || 'عادي'),
    status: t.status || 'Pending',
    assignedFrom: t.assignedFrom || '—',
    isOverdue: t.isOverdue || false,
  }
}

// ─── Process diagram ───────────────────────────────────────────────────────────
function ProcessDiagram({ workflow }) {
  const ds = workflow?.doneSteps ?? workflow?.done ?? 0
  const total = workflow?.steps ?? 3
  const stepNames = ['بدء','مراجعة','اعتماد','مكتمل'].slice(0, Math.min(total + 1, 4))

  return (
    <div className="flex items-center gap-1 mt-2 overflow-x-auto pb-1">
      {stepNames.map((name, i) => {
        const isDone = i === 0 || i <= ds
        const isActive = !isDone && i === ds + 1
        return (
          <React.Fragment key={i}>
            <div className="flex flex-col items-center flex-shrink-0">
              <div className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold ${
                isDone ? 'bg-green-500 text-white' : isActive ? 'bg-blue-600 text-white ring-2 ring-blue-100' : 'bg-gray-100 text-gray-400'
              }`}>
                {isDone ? '✓' : i + 1}
              </div>
              <p className="text-[9px] text-gray-400 mt-0.5 whitespace-nowrap">{name}</p>
            </div>
            {i < stepNames.length - 1 && (
              <div className={`flex-1 h-0.5 min-w-[16px] mt-[-10px] ${isDone ? 'bg-green-300' : 'bg-gray-100'}`} />
            )}
          </React.Fragment>
        )
      })}
    </div>
  )
}

// ─── Main ──────────────────────────────────────────────────────────────────────
export default function WorkflowsPage() {
  const { activeUsers: allUsers } = useUsers()
  const { show, ToastContainer } = useToast()

  const [tasks, setTasks]               = useLocalStorage('ecm_inbox_tasks', MOCK_TASKS)
  const [myWorkflows, setMyWorkflows]   = useLocalStorage('ecm_my_workflows', MOCK_WORKFLOWS)
  const [view, setView]                 = useState('inbox')   // inbox | my-workflows
  const [selected, setSelected]         = useState(null)
  const [selWorkflow, setSelWorkflow]   = useState(null)
  const [comment, setComment]           = useState('')
  const [delegateTo, setDelegateTo]     = useState(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [showBuilder, setShowBuilder]   = useState(false)
  const [wfFilter, setWfFilter]         = useState('all')

  // Ensure tasks is always an array (localStorage might corrupt)
  const safeTasks     = Array.isArray(tasks) ? tasks : MOCK_TASKS
  const safeWorkflows = Array.isArray(myWorkflows) ? myWorkflows : MOCK_WORKFLOWS

  useEffect(() => {
    client.get('/api/v1/workflow/inbox')
      .then(r => {
        const d = r.data?.data?.items || r.data?.data || r.data
        if (Array.isArray(d) && d.length > 0) setTasks(d.map(normalize))
      }).catch(() => {})

    client.get('/api/v1/workflow/my-workflows')
      .then(r => {
        const d = r.data?.data?.items || r.data?.data || r.data
        if (Array.isArray(d) && d.length > 0) setMyWorkflows(d)
      }).catch(() => {})
  }, [])

  const removeTask = (id) => {
    setTasks(p => (Array.isArray(p) ? p : []).filter(t => t.id !== id))
    setSelected(null); setComment(''); setDelegateTo(null)
  }

  const handleAction = async (action) => {
    if (!selected) return
    if (action === 'reject' && !comment.trim()) { show('يجب إدخال سبب الرفض', 'error'); return }
    setActionLoading(true)
    try {
      const id = selected.taskId || selected.id
      if (action === 'approve')  await client.post(`/api/v1/workflow/tasks/${id}/approve`, { comment })
      if (action === 'reject')   await client.post(`/api/v1/workflow/tasks/${id}/reject`,  { comment })
      if (action === 'delegate') await client.post(`/api/v1/workflow/tasks/${id}/delegate`, { toUserId: delegateTo?.userId, comment })
      if (action === 'claim')    await client.post(`/api/v1/workflow/tasks/${id}/claim`)
      if (action === 'return')   await client.post(`/api/v1/workflow/tasks/${id}/return-to-group`, { comment })
      const msgs = { approve:'✅ تمت الموافقة', reject:'تم الرفض', delegate:'✅ تمت الإحالة', claim:'✅ تمت المطالبة', return:'تم الإرجاع للمجموعة' }
      show(msgs[action] || 'تم التنفيذ', 'success')
      removeTask(selected.id)
    } catch (err) {
      show(err.response?.data?.message || 'تعذر تنفيذ الإجراء', 'error')
    } finally { setActionLoading(false) }
  }

  const handleCancelWorkflow = async (wf) => {
    try { await client.post(`/api/v1/workflow/instances/${wf.instanceId}/cancel`, { reason:'إلغاء' }) } catch {}
    setMyWorkflows(p => (Array.isArray(p) ? p : []).filter(w => w.instanceId !== wf.instanceId))
    setSelWorkflow(null)
    show('تم إلغاء سير العمل', 'success')
  }

  const handleDeleteWorkflow = async (wf) => {
    try { await client.delete(`/api/v1/workflow/instances/${wf.instanceId}`) } catch {}
    setMyWorkflows(p => (Array.isArray(p) ? p : []).filter(w => w.instanceId !== wf.instanceId))
    setSelWorkflow(null)
    show('تم حذف سير العمل', 'success')
  }

  const pending  = safeTasks.filter(t => t.status !== 'Done')
  const overdue  = safeTasks.filter(t => t.isOverdue || t.status === 'Overdue')
  const filteredWf = safeWorkflows.filter(w => wfFilter === 'all' || w.status === wfFilter)

  return (
    <div className="space-y-4 max-w-5xl">
      <ToastContainer />

      {showBuilder && (
        <WorkflowBuilderModal
          onClose={() => setShowBuilder(false)}
          onSuccess={(msg, newTask) => {
            show(msg, 'success')
            setShowBuilder(false)
            if (newTask) setTasks(p => [newTask, ...(Array.isArray(p) ? p : [])])
            const wf = {
              instanceId: Date.now(),
              title: newTask?.title || 'سير عمل جديد',
              status: 'Active', progress: 0, steps: 1, doneSteps: 0,
              started: new Date().toISOString().split('T')[0],
              priority: newTask?.priority || 'مهم'
            }
            setMyWorkflows(p => [wf, ...(Array.isArray(p) ? p : [])])
            setView('my-workflows')
          }}
        />
      )}

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">المهام وسير العمل</h1>
          <p className="text-gray-400 text-sm">
            {pending.length} مهمة معلقة
            {overdue.length > 0 && <span className="text-red-500 mr-2">• {overdue.length} متأخرة</span>}
          </p>
        </div>
        <button onClick={() => setShowBuilder(true)}
          className="bg-blue-700 text-white text-sm px-4 py-2 rounded-xl hover:bg-blue-800 transition-colors flex items-center gap-2 shadow-sm">
          🚀 بناء سير عمل
        </button>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 bg-gray-100 p-1 rounded-xl w-fit">
        {[
          { key:'inbox',        label:'📥 صندوق المهام',  count: pending.length },
          { key:'my-workflows', label:'📊 سير أعمالي',    count: safeWorkflows.length },
        ].map(t => (
          <button key={t.key} onClick={() => setView(t.key)}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-all flex items-center gap-1.5 ${
              view === t.key ? 'bg-white shadow text-gray-900' : 'text-gray-500 hover:text-gray-700'
            }`}>
            {t.label}
            {t.count > 0 && (
              <span className={`text-[11px] px-1.5 py-0.5 rounded-full font-bold ${
                view === t.key ? 'bg-blue-600 text-white' : 'bg-gray-300 text-gray-600'
              }`}>{t.count}</span>
            )}
          </button>
        ))}
      </div>

      {/* ══ INBOX ══ */}
      {view === 'inbox' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {/* Task list */}
          <div className="space-y-2">
            {pending.length === 0 && (
              <div className="bg-white rounded-2xl border p-12 text-center">
                <div className="text-4xl mb-3">🎉</div>
                <p className="font-semibold text-gray-600">لا توجد مهام معلقة</p>
                <p className="text-gray-300 text-xs mt-1">أنجزت جميع مهامك!</p>
              </div>
            )}
            {safeTasks.map(task => {
              const isLate = task.isOverdue || task.status === 'Overdue'
              const isSelected = selected?.id === task.id
              return (
                <div key={task.id}
                  onClick={() => setSelected(isSelected ? null : task)}
                  className={`bg-white rounded-xl border-2 p-4 cursor-pointer transition-all ${
                    isSelected ? 'border-blue-500 shadow-md' :
                    isLate ? 'border-red-200 hover:border-red-300' :
                    'border-gray-100 hover:border-blue-200'
                  }`}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-gray-800 text-sm truncate">{task.title}</p>
                      <p className="text-gray-400 text-xs mt-0.5">من: {task.assignedFrom}</p>
                      {task.dueDate && (
                        <p className={`text-xs mt-0.5 font-medium ${isLate ? 'text-red-500' : 'text-gray-400'}`}>
                          {isLate && '⚠️ '}{new Date(task.dueDate).toLocaleDateString('ar-SA')}
                        </p>
                      )}
                    </div>
                    <div className="flex flex-col items-end gap-1.5 flex-shrink-0">
                      <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${PRIORITY_CLS[task.priority] || 'bg-gray-100 text-gray-500'}`}>
                        {task.priority}
                      </span>
                      <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${isLate ? 'bg-red-50 text-red-600' : 'bg-blue-50 text-blue-600'}`}>
                        {isLate ? 'متأخر' : 'معلق'}
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
                <button onClick={() => setSelected(null)} className="text-gray-300 hover:text-gray-600 text-xl">✕</button>
              </div>

              {/* Info */}
              <div className="bg-gray-50 rounded-xl p-3 mb-4 space-y-1.5 text-xs">
                <div className="flex justify-between"><span className="text-gray-400">رقم المهمة</span><span className="font-mono">#{selected.taskId}</span></div>
                {selected.instanceId && <div className="flex justify-between"><span className="text-gray-400">سير العمل</span><span className="font-mono">#{selected.instanceId}</span></div>}
                <div className="flex justify-between"><span className="text-gray-400">النوع</span><span>{selected.type}</span></div>
                {selected.dueDate && (
                  <div className="flex justify-between">
                    <span className="text-gray-400">الموعد</span>
                    <span className={selected.isOverdue ? 'text-red-500 font-medium' : ''}>
                      {new Date(selected.dueDate).toLocaleDateString('ar-SA')}
                    </span>
                  </div>
                )}
              </div>

              {/* Comment */}
              <div className="mb-3">
                <label className="block text-xs font-bold text-gray-600 mb-1.5">التعليق (مطلوب عند الرفض)</label>
                <textarea value={comment} onChange={e => setComment(e.target.value)} rows={2}
                  placeholder="أضف تعليقاً..."
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none" />
              </div>

              {/* Delegate search */}
              <div className="mb-4">
                <label className="block text-xs font-bold text-gray-600 mb-1.5">إحالة إلى (اختياري)</label>
                {delegateTo ? (
                  <div className="flex items-center gap-2 p-2 bg-blue-50 rounded-xl border border-blue-200">
                    <div className="w-6 h-6 bg-blue-600 rounded-full flex items-center justify-center text-white text-xs font-bold">{delegateTo.fullNameAr?.[0]}</div>
                    <span className="flex-1 text-xs font-medium text-blue-800">{delegateTo.fullNameAr}</span>
                    <button onClick={() => setDelegateTo(null)} className="text-blue-300 hover:text-red-500 text-sm">✕</button>
                  </div>
                ) : (
                  <input
                    placeholder="اسم الموظف للإحالة..."
                    onFocus={() => {}}
                    onChange={e => {
                      // Simple: type to search from mock
                      const q = e.target.value
                      if (q.length >= 1) {
                        const mock = [
                          { userId:2, fullNameAr:'أحمد الزهراني',  email:'a.zahrani@darah.gov.sa' },
                          { userId:3, fullNameAr:'مريم العنزي',    email:'m.anzi@darah.gov.sa' },
                          { userId:4, fullNameAr:'خالد القحطاني',  email:'k.qahtani@darah.gov.sa' },
                        ].find(u => u.fullNameAr.includes(q))
                        if (mock) setDelegateTo(mock)
                      }
                    }}
                    className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
                  />
                )}
              </div>

              {/* Action buttons */}
              <div className="space-y-2">
                <div className="grid grid-cols-2 gap-2">
                  <button onClick={() => handleAction('approve')} disabled={actionLoading}
                    className="bg-green-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-green-700 disabled:opacity-50 flex items-center justify-center gap-1.5 transition-colors">
                    {actionLoading ? '⏳' : '✅'} موافقة
                  </button>
                  <button onClick={() => handleAction('reject')} disabled={actionLoading}
                    className="bg-red-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-red-700 disabled:opacity-50 flex items-center justify-center gap-1.5 transition-colors">
                    {actionLoading ? '⏳' : '❌'} رفض
                  </button>
                </div>
                <div className="grid grid-cols-3 gap-2">
                  <button onClick={() => handleAction('delegate')} disabled={actionLoading || !delegateTo}
                    className="border-2 border-blue-200 text-blue-700 py-2 rounded-xl text-xs font-bold hover:bg-blue-50 disabled:opacity-40 transition-colors">
                    ↗️ إحالة
                  </button>
                  <button onClick={() => handleAction('claim')} disabled={actionLoading}
                    className="border-2 border-purple-200 text-purple-700 py-2 rounded-xl text-xs font-bold hover:bg-purple-50 disabled:opacity-40 transition-colors">
                    🙋 مطالبة
                  </button>
                  <button onClick={() => handleAction('return')} disabled={actionLoading}
                    className="border-2 border-gray-200 text-gray-600 py-2 rounded-xl text-xs font-bold hover:bg-gray-50 disabled:opacity-40 transition-colors">
                    ↩️ إرجاع
                  </button>
                </div>
              </div>
            </div>
          ) : pending.length > 0 && (
            <div className="bg-white rounded-xl border-2 border-dashed border-gray-200 flex items-center justify-center p-12">
              <div className="text-center text-gray-400">
                <div className="text-3xl mb-3">👆</div>
                <p className="text-sm">اختر مهمة لاتخاذ إجراء</p>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ══ MY WORKFLOWS ══ */}
      {view === 'my-workflows' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {/* Workflows list */}
          <div className="space-y-3">
            <div className="flex gap-2">
              {[{v:'all',l:'الكل'},{v:'Active',l:'نشط'},{v:'Completed',l:'مكتمل'}].map(f => (
                <button key={f.v} onClick={() => setWfFilter(f.v)}
                  className={`text-xs px-3 py-1.5 rounded-lg font-medium transition-colors ${
                    wfFilter === f.v ? 'bg-blue-700 text-white' : 'border border-gray-200 text-gray-600 hover:bg-gray-50'
                  }`}>
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
              const ds = wf.doneSteps ?? wf.done ?? 0
              const total = wf.steps ?? 1
              const pct = total > 0 ? Math.round((ds / total) * 100) : 0
              const isSelected = selWorkflow?.instanceId === wf.instanceId
              return (
                <div key={wf.instanceId}
                  onClick={() => setSelWorkflow(isSelected ? null : wf)}
                  className={`bg-white rounded-xl border-2 p-4 cursor-pointer transition-all ${
                    isSelected ? 'border-blue-500 shadow-md' : 'border-gray-100 hover:border-blue-200'
                  }`}>
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-gray-800 text-sm truncate">{wf.title}</p>
                      <p className="text-gray-400 text-xs mt-0.5">#{wf.instanceId} • {new Date(wf.started).toLocaleDateString('ar-SA')}</p>
                    </div>
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium mr-2 flex-shrink-0 ${STATUS_CLS[wf.status] || 'bg-gray-50 text-gray-500'}`}>
                      {wf.status === 'Active' ? '🔵 نشط' : wf.status === 'Completed' ? '✅ مكتمل' : '⭕ ملغى'}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div className={`h-full rounded-full transition-all ${wf.status === 'Completed' ? 'bg-green-500' : 'bg-blue-500'}`}
                        style={{ width: `${pct}%` }} />
                    </div>
                    <span className="text-xs text-gray-400">{ds}/{total}</span>
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
                  <p className="text-gray-400 text-xs">#{selWorkflow.instanceId}</p>
                </div>
                <button onClick={() => setSelWorkflow(null)} className="text-gray-300 hover:text-gray-600 text-xl">✕</button>
              </div>

              <div className="bg-gray-50 rounded-xl p-3 mb-4 space-y-1.5 text-xs">
                <div className="flex justify-between"><span className="text-gray-400">الحالة</span><span className={STATUS_CLS[selWorkflow.status]?.split(' ')[1]}>{selWorkflow.status === 'Active' ? 'نشط' : selWorkflow.status === 'Completed' ? 'مكتمل' : 'ملغى'}</span></div>
                <div className="flex justify-between"><span className="text-gray-400">البداية</span><span>{new Date(selWorkflow.started).toLocaleDateString('ar-SA')}</span></div>
                <div className="flex justify-between"><span className="text-gray-400">التقدم</span><span>{selWorkflow.doneSteps ?? selWorkflow.done ?? 0} من {selWorkflow.steps ?? 1} خطوات</span></div>
                <div className="flex justify-between"><span className="text-gray-400">الأولوية</span><span>{selWorkflow.priority}</span></div>
              </div>

              <div className="mb-4">
                <p className="text-xs font-bold text-gray-500 mb-2">مخطط سير العمل</p>
                <div className="bg-gray-50 rounded-xl p-3">
                  <ProcessDiagram workflow={selWorkflow} />
                </div>
              </div>

              <div className="space-y-2">
                {selWorkflow.status === 'Active' && (
                  <button onClick={() => handleCancelWorkflow(selWorkflow)}
                    className="w-full border-2 border-red-200 text-red-600 py-2.5 rounded-xl text-sm font-bold hover:bg-red-50 transition-colors">
                    ⛔ إلغاء سير العمل
                  </button>
                )}
                {selWorkflow.status === 'Completed' && (
                  <button onClick={() => handleDeleteWorkflow(selWorkflow)}
                    className="w-full border-2 border-gray-200 text-gray-600 py-2.5 rounded-xl text-sm font-bold hover:bg-gray-50 transition-colors">
                    🗑️ حذف سير العمل
                  </button>
                )}
              </div>
            </div>
          ) : filteredWf.length > 0 && (
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
