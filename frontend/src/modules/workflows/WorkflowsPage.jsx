import React, { useState, useEffect } from 'react'
import client from '../../api/client'

const MOCK_TASKS = [
  { id: 1, title: 'مراجعة عقد التوريد السنوي', type: 'Approval', dueDate: '2026-04-14', priority: 1, status: 'Pending', assignedFrom: 'أحمد المطيري' },
  { id: 2, title: 'اعتماد تقرير الميزانية Q1', type: 'Review', dueDate: '2026-04-13', priority: 2, status: 'Pending', assignedFrom: 'مريم العتيبي' },
  { id: 3, title: 'التحقق من سياسة الخصوصية المحدثة', type: 'Approval', dueDate: '2026-04-20', priority: 0, status: 'Pending', assignedFrom: 'النظام' },
  { id: 4, title: 'مراجعة طلب الإجازة #2041', type: 'HR', dueDate: '2026-04-12', priority: 2, status: 'Overdue', assignedFrom: 'فهد القحطاني' },
]

const PRIORITY_MAP = {
  0: { label: 'عادي', cls: 'bg-gray-100 text-gray-600' },
  1: { label: 'مهم', cls: 'bg-yellow-100 text-yellow-700' },
  2: { label: 'عاجل', cls: 'bg-red-100 text-red-700' },
}

const TYPE_ICON = { Approval: '✅', Review: '👁', HR: '👤' }

export default function WorkflowsPage() {
  const [tasks, setTasks] = useState(MOCK_TASKS)
  const [selected, setSelected] = useState(null)
  const [comment, setComment] = useState('')
  const [actionLoading, setActionLoading] = useState(false)

  useEffect(() => {
    client.get('/api/v1/workflow/inbox').then(res => {
      const data = res.data?.data || res.data
      if (Array.isArray(data) && data.length > 0) setTasks(data)
    }).catch(() => {})
  }, [])

  const handleAction = async (action) => {
    if (!selected) return
    setActionLoading(true)
    try {
      if (action === 'approve') {
        await client.post(`/api/v1/workflow/tasks/${selected.id}/approve`, { comment })
      } else if (action === 'reject') {
        await client.post(`/api/v1/workflow/tasks/${selected.id}/reject`, { comment })
      }
      setTasks(t => t.filter(x => x.id !== selected.id))
      setSelected(null)
      setComment('')
    } catch (err) {
      const msg = err.response?.data?.message || err.response?.data?.errors?.[0] || 'تعذر تنفيذ الإجراء'
      alert(msg)
    } finally {
      setActionLoading(false)
    }
  }

  const isOverdue = (date) => new Date(date) < new Date()

  return (
    <div className="space-y-4 max-w-7xl">
      <div>
        <h1 className="text-xl font-bold text-gray-900">صندوق المهام</h1>
        <p className="text-gray-500 text-sm">{tasks.filter(t => t.status === 'Pending').length} مهمة معلقة</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {tasks.length === 0 ? (
          <div className="lg:col-span-2 bg-white rounded-xl border border-gray-100 p-12 text-center">
            <div className="text-5xl mb-3">🎉</div>
            <p className="text-gray-500">لا توجد مهام معلقة</p>
          </div>
        ) : (
          tasks.map(task => {
            const priority = PRIORITY_MAP[task.priority] || PRIORITY_MAP[0]
            const overdue = isOverdue(task.dueDate)
            return (
              <div key={task.id}
                className={`bg-white rounded-xl border shadow-sm p-5 cursor-pointer hover:shadow-md transition-shadow
                  ${selected?.id === task.id ? 'border-primary-500 ring-1 ring-primary-500' : 'border-gray-100'}
                  ${overdue ? 'border-r-4 border-r-red-400' : ''}`}
                onClick={() => setSelected(task)}>
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-start gap-3 flex-1">
                    <div className="w-9 h-9 bg-blue-50 rounded-lg flex items-center justify-center text-lg flex-shrink-0">
                      {TYPE_ICON[task.type] || '📋'}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-medium text-gray-800 text-sm leading-snug">{task.title || task.titleAr}</p>
                      <p className="text-xs text-gray-400 mt-1">من: {task.assignedFrom || task.assignedBy}</p>
                    </div>
                  </div>
                  <span className={`text-xs px-2 py-0.5 rounded-full flex-shrink-0 ${priority.cls}`}>
                    {priority.label}
                  </span>
                </div>
                <div className="flex items-center justify-between mt-3 pt-3 border-t border-gray-50">
                  <span className={`text-xs ${overdue ? 'text-red-600 font-medium' : 'text-gray-400'}`}>
                    {overdue ? '⚠️ متأخر — ' : '📅 '} {task.dueDate}
                  </span>
                  <div className="flex gap-2">
                    <button
                      onClick={e => { e.stopPropagation(); setSelected(task) }}
                      className="text-xs bg-green-600 text-white px-3 py-1 rounded-lg hover:bg-green-700 transition-colors">
                      موافقة
                    </button>
                    <button
                      onClick={e => { e.stopPropagation(); setSelected(task) }}
                      className="text-xs border border-red-300 text-red-600 px-3 py-1 rounded-lg hover:bg-red-50 transition-colors">
                      رفض
                    </button>
                  </div>
                </div>
              </div>
            )
          })
        )}
      </div>

      {/* Action panel */}
      {selected && (
        <div className="bg-white rounded-xl border border-primary-200 shadow-sm p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-gray-800">اتخاذ إجراء: {selected.title || selected.titleAr}</h2>
            <button onClick={() => setSelected(null)} className="text-gray-400 hover:text-gray-600">✕</button>
          </div>
          <textarea
            value={comment}
            onChange={e => setComment(e.target.value)}
            placeholder="أضف تعليقاً أو ملاحظة (اختياري)..."
            className="w-full border border-gray-200 rounded-lg p-3 text-sm text-right resize-none h-20 focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
          <div className="flex gap-3 mt-3">
            <button
              onClick={() => handleAction('approve')}
              disabled={actionLoading}
              className="flex-1 bg-green-600 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-green-700 disabled:opacity-50">
              ✅ موافقة
            </button>
            <button
              onClick={() => handleAction('reject')}
              disabled={actionLoading}
              className="flex-1 bg-red-600 text-white py-2.5 rounded-lg text-sm font-medium hover:bg-red-700 disabled:opacity-50">
              ❌ رفض
            </button>
            <button
              disabled={actionLoading}
              className="flex-1 border border-gray-300 text-gray-600 py-2.5 rounded-lg text-sm font-medium hover:bg-gray-50">
              ↗ إحالة
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
