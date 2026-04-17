import React, { useState, useEffect } from 'react'
import client from '../../api/client'
import { useToast } from '../../components/Toast'

const MOCK_TASKS = [
  { id:1, taskId:1, title:'مراجعة عقد التوريد السنوي', type:'Approval', dueDate:'2026-04-14', priority:'عاجل', status:'Pending', assignedFrom:'أحمد المطيري', instanceId:1 },
  { id:2, taskId:2, title:'اعتماد تقرير الميزانية Q1', type:'Review', dueDate:'2026-04-13', priority:'مهم', status:'Pending', assignedFrom:'مريم العتيبي', instanceId:2 },
  { id:3, taskId:3, title:'التحقق من سياسة الخصوصية المحدثة', type:'Approval', dueDate:'2026-04-20', priority:'عادي', status:'Pending', assignedFrom:'النظام', instanceId:3 },
  { id:4, taskId:4, title:'مراجعة طلب الإجازة #2041', type:'HR', dueDate:'2026-04-12', priority:'عاجل', status:'Overdue', assignedFrom:'فهد القحطاني', instanceId:4 },
]

const PRIORITY_CLS = {
  'عاجل': 'bg-red-100 text-red-700',
  'مهم':  'bg-yellow-100 text-yellow-700',
  'عادي': 'bg-gray-100 text-gray-600',
  2: 'bg-red-100 text-red-700',
  1: 'bg-yellow-100 text-yellow-700',
  0: 'bg-gray-100 text-gray-600',
}

const TYPE_ICON = { Approval:'✅', Review:'👁️', HR:'👤', approval:'✅', review:'👁️' }
const STATUS_CLS = {
  Pending: 'bg-blue-100 text-blue-700',
  Overdue: 'bg-red-100 text-red-700',
  Done:    'bg-green-100 text-green-700',
}

// Normalize API response to consistent shape
function normalize(t) {
  return {
    id:           t.taskId || t.id || t.TaskId,
    taskId:       t.taskId || t.id || t.TaskId,
    instanceId:   t.instanceId || t.InstanceId,
    title:        t.title || t.documentTitleAr || t.DocumentTitleAr || t.stépNameAr || 'مهمة',
    type:         t.type || t.documentTypeName || t.DocumentTypeName || 'Review',
    dueDate:      t.dueDate || t.dueAt || t.DueAt,
    priority:     t.priority ?? t.Priority ?? 'عادي',
    status:       t.status || t.Status || 'Pending',
    assignedFrom: t.assignedFrom || t.documentNumber || t.DocumentNumber || '—',
    isOverdue:    t.isOverdue || t.IsOverdue || false,
  }
}

export default function WorkflowsPage() {
  const [tasks, setTasks]             = useState(MOCK_TASKS)
  const [selected, setSelected]       = useState(null)
  const [comment, setComment]         = useState('')
  const [actionLoading, setActionLoading] = useState(false)
  const [delegateTo, setDelegateTo]   = useState('')
  const { show, ToastContainer }      = useToast()

  useEffect(() => {
    client.get('/api/v1/workflow/inbox')
      .then(res => {
        const data = res.data?.data?.items || res.data?.data || res.data
        if (Array.isArray(data) && data.length > 0)
          setTasks(data.map(normalize))
      })
      .catch(() => {}) // keep mock
  }, [])

  const removeTask = (id) => {
    setTasks(t => t.filter(x => x.id !== id))
    setSelected(null)
    setComment('')
    setDelegateTo('')
  }

  const handleAction = async (action) => {
    if (!selected) return
    setActionLoading(true)
    try {
      const taskId = selected.taskId || selected.id

      if (action === 'approve') {
        await client.post(`/api/v1/workflow/tasks/${taskId}/approve`, { comment })
        show('تمت الموافقة بنجاح', 'success')
      } else if (action === 'reject') {
        if (!comment.trim()) { show('يجب إدخال سبب الرفض', 'error'); return }
        await client.post(`/api/v1/workflow/tasks/${taskId}/reject`, { comment })
        show('تم الرفض بنجاح', 'success')
      } else if (action === 'delegate') {
        if (!delegateTo.trim()) { show('يجب تحديد المستخدم المُحال إليه', 'error'); return }
        await client.post(`/api/v1/workflow/tasks/${taskId}/delegate`, { toUserId: Number(delegateTo), comment })
        show('تمت الإحالة بنجاح', 'success')
      }
      removeTask(selected.id)
    } catch (err) {
      const msg = err.response?.data?.message || err.response?.data?.errors?.[0] || 'تعذر تنفيذ الإجراء'
      show(msg, 'error')
    } finally {
      setActionLoading(false)
    }
  }

  const pending  = tasks.filter(t => t.status !== 'Done')
  const overdue  = tasks.filter(t => t.isOverdue || t.status === 'Overdue')

  return (
    <div className="space-y-4 max-w-5xl">
      <ToastContainer />

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">صندوق المهام</h1>
          <p className="text-gray-400 text-sm">
            {pending.length} مهمة معلقة
            {overdue.length > 0 && <span className="text-red-500 mr-2">• {overdue.length} متأخرة</span>}
          </p>
        </div>
        <div className="flex gap-2 text-xs">
          {[
            { label:`الكل (${tasks.length})`, val:'all' },
            { label:`معلق (${pending.length})`, val:'Pending' },
            { label:`متأخر (${overdue.length})`, val:'Overdue' },
          ].map(f => (
            <button key={f.val} className="border border-gray-200 px-3 py-1.5 rounded-lg text-gray-600 hover:bg-gray-50 transition-colors">
              {f.label}
            </button>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Task list */}
        <div className="space-y-3">
          {tasks.length === 0 && (
            <div className="bg-white rounded-xl border border-gray-100 p-12 text-center">
              <div className="text-4xl mb-3">🎉</div>
              <p className="text-gray-500 font-medium">لا توجد مهام معلقة</p>
              <p className="text-gray-300 text-xs mt-1">أنجزت جميع مهامك!</p>
            </div>
          )}

          {tasks.map(task => {
            const isSelected = selected?.id === task.id
            const isLate = task.isOverdue || task.status === 'Overdue'
            const pCls = PRIORITY_CLS[task.priority] || 'bg-gray-100 text-gray-600'
            const sCls = STATUS_CLS[task.status] || 'bg-gray-100 text-gray-500'

            return (
              <div
                key={task.id}
                onClick={() => isSelected ? setSelected(null) : setSelected(task)}
                className={`bg-white rounded-xl border-2 p-4 cursor-pointer transition-all ${
                  isSelected ? 'border-blue-500 shadow-md' :
                  isLate ? 'border-red-200 hover:border-red-300' :
                  'border-gray-100 hover:border-blue-200'
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-start gap-3 flex-1 min-w-0">
                    <span className="text-xl mt-0.5 flex-shrink-0">{TYPE_ICON[task.type] || '📋'}</span>
                    <div className="min-w-0">
                      <p className="font-semibold text-gray-800 text-sm leading-snug">{task.title}</p>
                      <p className="text-gray-400 text-xs mt-1">من: {task.assignedFrom}</p>
                      {task.dueDate && (
                        <p className={`text-xs mt-1 font-medium ${isLate ? 'text-red-500' : 'text-gray-400'}`}>
                          {isLate ? '⚠️ متأخر —' : '📅'} {new Date(task.dueDate).toLocaleDateString('ar-SA')}
                        </p>
                      )}
                    </div>
                  </div>
                  <div className="flex flex-col items-end gap-1.5 flex-shrink-0">
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${pCls}`}>
                      {typeof task.priority === 'number'
                        ? ['عادي','مهم','عاجل'][task.priority] || 'عادي'
                        : task.priority}
                    </span>
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${sCls}`}>
                      {task.status === 'Pending' ? 'معلق' : task.status === 'Overdue' ? 'متأخر' : task.status}
                    </span>
                  </div>
                </div>
              </div>
            )
          })}
        </div>

        {/* Action panel */}
        {selected && (
          <div className="bg-white rounded-xl border-2 border-blue-200 p-5 shadow-sm">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="font-bold text-gray-900 text-sm">اتخاذ إجراء</h3>
                <p className="text-gray-400 text-xs mt-0.5 truncate max-w-[200px]">{selected.title}</p>
              </div>
              <button onClick={() => setSelected(null)} className="text-gray-300 hover:text-gray-500 text-xl">✕</button>
            </div>

            {/* Comment */}
            <div className="mb-4">
              <label className="block text-xs font-semibold text-gray-600 mb-1.5">التعليق / الملاحظة</label>
              <textarea
                value={comment}
                onChange={e => setComment(e.target.value)}
                rows={3}
                placeholder="أضف تعليقاً (مطلوب عند الرفض)..."
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"
              />
            </div>

            {/* Delegate field */}
            <div className="mb-4">
              <label className="block text-xs font-semibold text-gray-600 mb-1.5">إحالة إلى (رقم المستخدم)</label>
              <input
                type="number"
                value={delegateTo}
                onChange={e => setDelegateTo(e.target.value)}
                placeholder="أدخل رقم المستخدم للإحالة..."
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
              />
            </div>

            {/* Action buttons */}
            <div className="space-y-2">
              <div className="grid grid-cols-2 gap-2">
                <button
                  onClick={() => handleAction('approve')}
                  disabled={actionLoading}
                  className="bg-green-600 text-white py-2.5 rounded-xl text-sm font-semibold hover:bg-green-700 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors"
                >
                  {actionLoading ? '⏳' : '✅'} موافقة
                </button>
                <button
                  onClick={() => handleAction('reject')}
                  disabled={actionLoading}
                  className="bg-red-600 text-white py-2.5 rounded-xl text-sm font-semibold hover:bg-red-700 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors"
                >
                  {actionLoading ? '⏳' : '❌'} رفض
                </button>
              </div>
              <button
                onClick={() => handleAction('delegate')}
                disabled={actionLoading || !delegateTo}
                className="w-full border-2 border-blue-200 text-blue-700 py-2.5 rounded-xl text-sm font-semibold hover:bg-blue-50 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors"
              >
                {actionLoading ? '⏳' : '↗️'} إحالة
              </button>
            </div>

            {/* Task info */}
            <div className="mt-4 pt-4 border-t border-gray-100 space-y-1.5">
              <div className="flex justify-between text-xs">
                <span className="text-gray-400">رقم المهمة</span>
                <span className="font-mono text-gray-600">#{selected.taskId}</span>
              </div>
              {selected.instanceId && (
                <div className="flex justify-between text-xs">
                  <span className="text-gray-400">رقم سير العمل</span>
                  <span className="font-mono text-gray-600">#{selected.instanceId}</span>
                </div>
              )}
              <div className="flex justify-between text-xs">
                <span className="text-gray-400">النوع</span>
                <span className="text-gray-600">{selected.type}</span>
              </div>
              {selected.dueDate && (
                <div className="flex justify-between text-xs">
                  <span className="text-gray-400">الموعد النهائي</span>
                  <span className={selected.isOverdue ? 'text-red-500 font-medium' : 'text-gray-600'}>
                    {new Date(selected.dueDate).toLocaleDateString('ar-SA')}
                  </span>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Empty state when no task selected */}
        {!selected && tasks.length > 0 && (
          <div className="bg-white rounded-xl border-2 border-dashed border-gray-200 flex items-center justify-center p-12">
            <div className="text-center text-gray-400">
              <div className="text-3xl mb-3">👆</div>
              <p className="text-sm">اختر مهمة لاتخاذ إجراء</p>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
