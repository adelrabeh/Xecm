import React, { useState, useRef } from 'react'
import { UserSearch } from './UserSearch'
import client from '../api/client'

// ─── Step action types ─────────────────────────────────────────────────────────
const ACTIONS = [
  { value:'review',    label:'مراجعة',           icon:'👁️', color:'bg-blue-50 border-blue-300 text-blue-700',   desc:'مراجعة المحتوى وإبداء الملاحظات' },
  { value:'spelling',  label:'مراجعة إملائية',   icon:'🔤', color:'bg-purple-50 border-purple-300 text-purple-700', desc:'تدقيق الإملاء واللغة' },
  { value:'language',  label:'مراجعة لغوية',      icon:'✍️', color:'bg-indigo-50 border-indigo-300 text-indigo-700', desc:'مراجعة الأسلوب والبنية اللغوية' },
  { value:'audit',     label:'تدقيق',             icon:'🔍', color:'bg-yellow-50 border-yellow-300 text-yellow-700', desc:'التدقيق الشامل والتحقق' },
  { value:'approve',   label:'اعتماد',            icon:'✅', color:'bg-green-50 border-green-300 text-green-700',   desc:'اعتماد الوثيقة رسمياً' },
  { value:'sign',      label:'توقيع',             icon:'🖊️', color:'bg-teal-50 border-teal-300 text-teal-700',     desc:'التوقيع الرسمي على الوثيقة' },
  { value:'print',     label:'طباعة',             icon:'🖨️', color:'bg-gray-50 border-gray-300 text-gray-700',     desc:'إرسال للطباعة الرسمية' },
  { value:'archive',   label:'أرشفة',             icon:'🗃️', color:'bg-orange-50 border-orange-300 text-orange-700', desc:'حفظ وأرشفة الوثيقة' },
  { value:'notify',    label:'إشعار / إعلام',     icon:'📢', color:'bg-pink-50 border-pink-300 text-pink-700',     desc:'إشعار الأطراف المعنية' },
  { value:'task',      label:'تكليف مهمة',        icon:'📋', color:'bg-cyan-50 border-cyan-300 text-cyan-700',     desc:'تكليف بمهمة محددة' },
]

const PRIORITIES = [
  { value:'low',    label:'عادي',  color:'text-gray-500' },
  { value:'medium', label:'مهم',   color:'text-yellow-600' },
  { value:'high',   label:'عاجل',  color:'text-red-600' },
]

const MOCK_DOCS = [
  { id:'d1', name:'تقرير الميزانية السنوي 2026.pdf',     type:'PDF'  },
  { id:'d2', name:'عقد توريد المستلزمات 2026.docx',      type:'DOCX' },
  { id:'d3', name:'سياسة حماية البيانات.pdf',            type:'PDF'  },
  { id:'d4', name:'خطة الاستمرارية التشغيلية.docx',      type:'DOCX' },
  { id:'d5', name:'محضر اجتماع مجلس الإدارة.pdf',       type:'PDF'  },
  { id:'d8', name:'عرض الرؤية الاستراتيجية.pptx',       type:'PPTX' },
]

const FILE_ICON = { PDF:'📕', DOCX:'📘', XLSX:'📗', PPTX:'📙', ZIP:'📦' }

// ─── Single workflow step ──────────────────────────────────────────────────────
function WorkflowStep({ step, index, total, onChange, onRemove, onMoveUp, onMoveDown }) {
  const [addingUser, setAddingUser] = useState(false)
  const action = ACTIONS.find(a => a.value === step.action) || ACTIONS[0]

  const addUser = (user) => {
    if (!user) return
    if (step.assignees.find(a => a.userId === user.userId)) return
    onChange({ ...step, assignees: [...step.assignees, user] })
    setAddingUser(false)
  }

  const removeUser = (userId) => {
    onChange({ ...step, assignees: step.assignees.filter(a => a.userId !== userId) })
  }

  return (
    <div className="border-2 border-gray-100 rounded-2xl bg-white overflow-hidden transition-all hover:border-blue-200">
      {/* Step header */}
      <div className="flex items-center gap-3 px-4 py-3 bg-gray-50/60 border-b border-gray-100">
        {/* Step number */}
        <div className="w-7 h-7 bg-blue-600 rounded-full flex items-center justify-center text-white text-xs font-black flex-shrink-0">
          {index + 1}
        </div>

        {/* Action selector */}
        <div className="flex-1 min-w-0">
          <select
            value={step.action}
            onChange={e => onChange({ ...step, action: e.target.value })}
            className="w-full text-sm font-semibold bg-transparent border-none outline-none text-gray-800 cursor-pointer">
            {ACTIONS.map(a => (
              <option key={a.value} value={a.value}>{a.icon} {a.label}</option>
            ))}
          </select>
        </div>

        {/* Reorder + remove */}
        <div className="flex items-center gap-1 flex-shrink-0">
          <button onClick={onMoveUp} disabled={index === 0}
            className="w-6 h-6 rounded flex items-center justify-center text-gray-400 hover:text-gray-600 disabled:opacity-30 text-sm">↑</button>
          <button onClick={onMoveDown} disabled={index === total - 1}
            className="w-6 h-6 rounded flex items-center justify-center text-gray-400 hover:text-gray-600 disabled:opacity-30 text-sm">↓</button>
          <button onClick={onRemove}
            className="w-6 h-6 rounded flex items-center justify-center text-gray-300 hover:text-red-500 text-lg">✕</button>
        </div>
      </div>

      <div className="p-4 space-y-3">
        {/* Action badge */}
        <div className={`inline-flex items-center gap-1.5 text-xs px-2.5 py-1 rounded-full border font-medium ${action.color}`}>
          <span>{action.icon}</span>
          <span>{action.desc}</span>
        </div>

        {/* Step title */}
        <input
          value={step.title}
          onChange={e => onChange({ ...step, title: e.target.value })}
          placeholder={`عنوان الخطوة — مثال: ${action.label} من المدير المالي`}
          className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"
        />

        {/* Instructions */}
        <textarea
          value={step.instructions}
          onChange={e => onChange({ ...step, instructions: e.target.value })}
          rows={2}
          placeholder="التعليمات للمُكلَّف — ماذا تريد منه بالتحديد؟"
          className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"
        />

        {/* Assignees */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <label className="text-xs font-bold text-gray-700">
              المُكلَّفون <span className="text-red-400">*</span>
            </label>
            {step.assignees.length > 1 && (
              <select value={step.routing} onChange={e => onChange({...step, routing: e.target.value})}
                className="text-[10px] border border-gray-200 rounded-lg px-1.5 py-0.5 focus:outline-none">
                <option value="sequential">تسلسلي — واحد تلو الآخر</option>
                <option value="parallel">متوازي — جميعهم في نفس الوقت</option>
                <option value="any">أي واحد — أول مَن يستجيب</option>
              </select>
            )}
          </div>

          {/* Assigned users */}
          {step.assignees.length > 0 && (
            <div className="space-y-1.5 mb-2">
              {step.assignees.map((u, i) => (
                <div key={u.userId} className="flex items-center gap-2 p-2 bg-blue-50 rounded-xl border border-blue-100">
                  {step.routing === 'sequential' && (
                    <span className="text-[10px] text-blue-400 font-bold w-4 text-center flex-shrink-0">{i+1}</span>
                  )}
                  <div className="w-6 h-6 bg-blue-600 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
                    {u.fullNameAr?.[0] || u.name?.[0]}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-xs font-semibold text-blue-800 truncate">{u.fullNameAr || u.name}</p>
                    <p className="text-[10px] text-blue-500 truncate">{u.email}</p>
                  </div>
                  <button onClick={() => removeUser(u.userId)}
                    className="text-blue-300 hover:text-red-500 text-sm transition-colors">✕</button>
                </div>
              ))}
            </div>
          )}

          {/* Add user */}
          {addingUser ? (
            <div className="border border-blue-200 rounded-xl p-2 bg-blue-50/50">
              <UserSearch
                onSelect={u => u && addUser(u)}
                selected={null}
                exclude={step.assignees.map(a => a.userId)}
                placeholder="ابحث عن موظف..."
              />
              <button onClick={() => setAddingUser(false)}
                className="mt-1 text-xs text-gray-400 hover:text-gray-600">إلغاء</button>
            </div>
          ) : (
            <button onClick={() => setAddingUser(true)}
              className="w-full border-2 border-dashed border-blue-200 text-blue-600 hover:border-blue-400 hover:bg-blue-50 rounded-xl py-2 text-xs font-semibold transition-colors flex items-center justify-center gap-1.5">
              <span>+</span> إضافة موظف
            </button>
          )}
        </div>

        {/* Due date */}
        <div className="flex items-center gap-3">
          <div className="flex-1">
            <label className="block text-[10px] font-bold text-gray-500 mb-1">موعد الإنجاز</label>
            <input type="date" value={step.dueDate}
              min={new Date().toISOString().split('T')[0]}
              onChange={e => onChange({...step, dueDate: e.target.value})}
              className="w-full border border-gray-200 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-blue-400" />
          </div>
          <div>
            <label className="block text-[10px] font-bold text-gray-500 mb-1">الأولوية</label>
            <select value={step.priority} onChange={e => onChange({...step, priority: e.target.value})}
              className="border border-gray-200 rounded-lg px-2 py-1.5 text-xs focus:outline-none">
              {PRIORITIES.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}
            </select>
          </div>
          <div className="pt-4">
            <label className="flex items-center gap-1.5 text-xs text-gray-600 cursor-pointer">
              <input type="checkbox" checked={step.required}
                onChange={e => onChange({...step, required: e.target.checked})}
                className="w-3.5 h-3.5 accent-blue-600" />
              إلزامي
            </label>
          </div>
        </div>
      </div>
    </div>
  )
}

// ─── Main modal ────────────────────────────────────────────────────────────────
export function WorkflowBuilderModal({ onClose, onSuccess }) {
  const [workflowTitle, setWorkflowTitle] = useState('')
  const [description, setDescription]    = useState('')
  const [priority, setPriority]          = useState('medium')
  const [dueDate, setDueDate]            = useState('')
  const [selectedDocs, setSelectedDocs]  = useState([])
  const [steps, setSteps]               = useState([
    { id:1, action:'review', title:'', instructions:'', assignees:[], dueDate:'', priority:'medium', required:true, routing:'sequential' }
  ])
  const [loading, setLoading]           = useState(false)
  const [error, setError]               = useState('')
  const [docSearch, setDocSearch]       = useState('')

  const nextId = useRef(2)

  const addStep = (afterIndex) => {
    const newStep = {
      id: nextId.current++,
      action: 'review', title: '', instructions: '',
      assignees: [], dueDate: '', priority: 'medium',
      required: true, routing: 'sequential'
    }
    const newSteps = [...steps]
    newSteps.splice(afterIndex + 1, 0, newStep)
    setSteps(newSteps)
  }

  const updateStep = (index, updated) => {
    setSteps(prev => prev.map((s, i) => i === index ? updated : s))
  }

  const removeStep = (index) => {
    if (steps.length === 1) return
    setSteps(prev => prev.filter((_, i) => i !== index))
  }

  const moveStep = (index, direction) => {
    const newSteps = [...steps]
    const target = index + direction
    if (target < 0 || target >= newSteps.length) return;
    [newSteps[index], newSteps[target]] = [newSteps[target], newSteps[index]]
    setSteps(newSteps)
  }

  const toggleDoc = (doc) => {
    setSelectedDocs(prev =>
      prev.find(d => d.id === doc.id)
        ? prev.filter(d => d.id !== doc.id)
        : [...prev, doc]
    )
  }

  const filteredDocs = MOCK_DOCS.filter(d =>
    d.name.includes(docSearch) || !docSearch
  )

  const validate = () => {
    if (!workflowTitle.trim()) return 'يجب إدخال عنوان سير العمل'
    for (let i = 0; i < steps.length; i++) {
      if (steps[i].assignees.length === 0)
        return `الخطوة ${i+1}: يجب تحديد موظف واحد على الأقل`
    }
    return null
  }

  const handleSubmit = async () => {
    const err = validate()
    if (err) { setError(err); return }
    setLoading(true); setError('')

    const payload = {
      title: workflowTitle,
      description,
      priority,
      dueDate,
      documents: selectedDocs.map(d => d.id),
      steps: steps.map((s, i) => ({
        order: i + 1,
        action: s.action,
        title: s.title || `الخطوة ${i+1} — ${ACTIONS.find(a=>a.value===s.action)?.label}`,
        instructions: s.instructions,
        assignees: s.assignees.map(a => a.userId),
        dueDate: s.dueDate,
        priority: s.priority,
        required: s.required,
        routing: s.routing,
      }))
    }

    try {
      await client.post('/api/v1/workflow/submit/00000000-0000-0000-0000-000000000001', {
        comment: JSON.stringify(payload)
      })
    } catch {}

    // Build new task for inbox
    const newTask = {
      id: Date.now(), taskId: Date.now(),
      instanceId: Date.now(),
      title: workflowTitle,
      type: ACTIONS.find(a => a.value === steps[0].action)?.label || 'مراجعة',
      dueDate: steps[0].dueDate || dueDate,
      priority: priority === 'high' ? 'عاجل' : priority === 'medium' ? 'مهم' : 'عادي',
      status: 'Pending',
      assignedFrom: 'أنت',
      isOverdue: false,
    }

    setLoading(false)
    onSuccess(
      `✅ تم إطلاق "${workflowTitle}" — ${steps.length} خطوة${selectedDocs.length ? ` على ${selectedDocs.length} وثيقة` : ''}`,
      newTask
    )
    onClose()
  }

  const totalAssignees = [...new Set(steps.flatMap(s => s.assignees.map(a => a.userId)))].length

  return (
    <div className="fixed inset-0 bg-black/60 flex items-start justify-center z-50 p-4 overflow-y-auto" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl my-4" onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="sticky top-0 bg-white rounded-t-2xl p-5 border-b border-gray-100 z-10">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="font-black text-gray-900 text-lg">بناء سير عمل</h2>
              <p className="text-xs text-gray-400 mt-0.5">
                {steps.length} خطوة · {totalAssignees} موظف
                {selectedDocs.length > 0 && ` · ${selectedDocs.length} وثيقة`}
              </p>
            </div>
            <button onClick={onClose} className="w-9 h-9 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 hover:text-gray-600 text-xl">✕</button>
          </div>
        </div>

        <div className="p-5 space-y-6">

          {/* ── Workflow info ── */}
          <div className="space-y-3">
            <input value={workflowTitle} onChange={e=>setWorkflowTitle(e.target.value)}
              placeholder="عنوان سير العمل — مثال: مراجعة واعتماد بحث 2026 *"
              className="w-full border-2 border-gray-200 focus:border-blue-500 rounded-xl px-4 py-3 text-sm font-semibold focus:outline-none text-right transition-colors"/>
            <textarea value={description} onChange={e=>setDescription(e.target.value)}
              rows={2} placeholder="وصف مختصر لسير العمل وهدفه (اختياري)..."
              className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"/>
            <div className="flex gap-3">
              <div className="flex-1">
                <label className="block text-[10px] font-bold text-gray-500 mb-1">الموعد النهائي الكلي</label>
                <input type="date" value={dueDate} min={new Date().toISOString().split('T')[0]}
                  onChange={e=>setDueDate(e.target.value)}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"/>
              </div>
              <div>
                <label className="block text-[10px] font-bold text-gray-500 mb-1">الأولوية</label>
                <div className="flex gap-1">
                  {PRIORITIES.map(p => (
                    <button key={p.value} onClick={() => setPriority(p.value)}
                      className={`px-3 py-2 rounded-xl text-xs font-bold border transition-all ${
                        priority===p.value ? 'bg-blue-600 text-white border-blue-600' : 'border-gray-200 text-gray-600 hover:bg-gray-50'
                      }`}>
                      {p.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </div>

          {/* ── Documents ── */}
          <div>
            <div className="flex items-center justify-between mb-2">
              <label className="text-sm font-bold text-gray-800">📎 الوثائق المرتبطة</label>
              <span className="text-xs text-gray-400">{selectedDocs.length} محدد</span>
            </div>
            <div className="relative mb-2">
              <input value={docSearch} onChange={e=>setDocSearch(e.target.value)}
                placeholder="ابحث في الوثائق..."
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right pr-9"/>
              <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
            </div>
            <div className="grid grid-cols-1 gap-1.5 max-h-40 overflow-y-auto">
              {filteredDocs.map(doc => {
                const sel = selectedDocs.find(d => d.id === doc.id)
                return (
                  <button key={doc.id} onClick={() => toggleDoc(doc)}
                    className={`flex items-center gap-2.5 px-3 py-2 rounded-xl border text-right transition-all ${
                      sel ? 'border-blue-400 bg-blue-50' : 'border-gray-100 hover:border-blue-200 hover:bg-gray-50'
                    }`}>
                    <span className="text-lg flex-shrink-0">{FILE_ICON[doc.type] || '📄'}</span>
                    <p className="flex-1 text-xs font-medium text-gray-700 truncate">{doc.name}</p>
                    <div className={`w-5 h-5 rounded-full border-2 flex items-center justify-center flex-shrink-0 transition-all ${
                      sel ? 'border-blue-500 bg-blue-500' : 'border-gray-300'
                    }`}>
                      {sel && <span className="text-white text-[10px] font-black">✓</span>}
                    </div>
                  </button>
                )
              })}
            </div>
          </div>

          {/* ── Steps ── */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <label className="text-sm font-bold text-gray-800">🔀 خطوات سير العمل</label>
              <span className="text-xs text-gray-400">التسلسل من الأعلى للأسفل</span>
            </div>

            <div className="space-y-3">
              {steps.map((step, index) => (
                <div key={step.id}>
                  <WorkflowStep
                    step={step}
                    index={index}
                    total={steps.length}
                    onChange={updated => updateStep(index, updated)}
                    onRemove={() => removeStep(index)}
                    onMoveUp={() => moveStep(index, -1)}
                    onMoveDown={() => moveStep(index, 1)}
                  />
                  {/* Add step between */}
                  <div className="flex items-center gap-2 my-2">
                    <div className="flex-1 h-px bg-gray-100"/>
                    <button onClick={() => addStep(index)}
                      className="flex items-center gap-1 text-[11px] text-blue-500 hover:text-blue-700 bg-blue-50 hover:bg-blue-100 border border-blue-200 px-2.5 py-1 rounded-full transition-colors font-semibold">
                      + إضافة خطوة هنا
                    </button>
                    <div className="flex-1 h-px bg-gray-100"/>
                  </div>
                </div>
              ))}
            </div>

            {/* Add last step */}
            <button onClick={() => addStep(steps.length - 1)}
              className="w-full border-2 border-dashed border-gray-200 hover:border-blue-300 text-gray-400 hover:text-blue-600 hover:bg-blue-50/50 rounded-2xl py-3 text-sm font-semibold transition-all flex items-center justify-center gap-2">
              <span className="text-lg">+</span> إضافة خطوة جديدة
            </button>
          </div>

          {/* ── Flow preview ── */}
          {steps.length > 0 && (
            <div className="bg-gray-50 rounded-2xl p-4">
              <p className="text-xs font-bold text-gray-500 mb-3">معاينة تسلسل سير العمل</p>
              <div className="flex flex-wrap items-center gap-2">
                {steps.map((s, i) => {
                  const act = ACTIONS.find(a => a.value === s.action)
                  return (
                    <React.Fragment key={s.id}>
                      <div className="flex flex-col items-center gap-1">
                        <div className={`px-2.5 py-1.5 rounded-xl border text-xs font-semibold ${act?.color || 'bg-gray-100 text-gray-600 border-gray-200'}`}>
                          {act?.icon} {act?.label}
                        </div>
                        {s.assignees.length > 0 && (
                          <div className="flex -space-x-1">
                            {s.assignees.slice(0,3).map(a => (
                              <div key={a.userId} title={a.fullNameAr || a.name}
                                className="w-5 h-5 bg-blue-600 rounded-full border border-white flex items-center justify-center text-white text-[9px] font-bold">
                                {(a.fullNameAr || a.name)?.[0]}
                              </div>
                            ))}
                            {s.assignees.length > 3 && (
                              <div className="w-5 h-5 bg-gray-400 rounded-full border border-white flex items-center justify-center text-white text-[9px]">
                                +{s.assignees.length-3}
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                      {i < steps.length - 1 && (
                        <span className="text-gray-300 text-lg">→</span>
                      )}
                    </React.Fragment>
                  )
                })}
                <span className="text-gray-300 text-lg">→</span>
                <div className="px-2.5 py-1.5 rounded-xl border bg-green-50 text-green-700 border-green-200 text-xs font-semibold">
                  🏁 مكتمل
                </div>
              </div>
            </div>
          )}

          {error && (
            <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-600 font-medium">
              ⚠️ {error}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="sticky bottom-0 bg-white rounded-b-2xl border-t border-gray-100 p-4 flex gap-3">
          <button onClick={onClose}
            className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50 transition-colors">
            إلغاء
          </button>
          <button onClick={handleSubmit} disabled={loading}
            className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors">
            {loading ? '⏳ جارٍ الإطلاق...' : `🚀 إطلاق سير العمل (${steps.length} خطوة)`}
          </button>
        </div>
      </div>
    </div>
  )
}
