import React, { useState, useRef } from 'react'
import { useLocalStorage } from '../../hooks/useLocalStorage'
import { useAuthStore } from '../../store/authStore'
import { useToast } from '../../components/Toast'
import client from '../../api/client'

// ─── Constants ────────────────────────────────────────────────────────────────
const STATUSES = [
  { key:'new',        label:'جديدة',         icon:'🆕', color:'#6366f1', bg:'#eef2ff', dot:'#818cf8' },
  { key:'assigned',   label:'مُسنَدة',        icon:'👤', color:'#0369a1', bg:'#eff6ff', dot:'#38bdf8' },
  { key:'inprogress', label:'قيد التنفيذ',   icon:'🔄', color:'#b45309', bg:'#fffbeb', dot:'#f59e0b' },
  { key:'review',     label:'قيد المراجعة',  icon:'🔍', color:'#7c3aed', bg:'#f5f3ff', dot:'#a78bfa' },
  { key:'completed',  label:'مكتملة',        icon:'✅', color:'#059669', bg:'#ecfdf5', dot:'#34d399' },
  { key:'overdue',    label:'متأخرة',         icon:'⚠️', color:'#dc2626', bg:'#fef2f2', dot:'#f87171' },
  { key:'cancelled',  label:'ملغاة',          icon:'⛔', color:'#6b7280', bg:'#f9fafb', dot:'#9ca3af' },
]
const PRIORITIES = [
  { key:'low',    label:'منخفضة', color:'#16a34a', bg:'#dcfce7' },
  { key:'medium', label:'متوسطة', color:'#d97706', bg:'#fef9c3' },
  { key:'high',   label:'عالية',  color:'#ea580c', bg:'#ffedd5' },
  { key:'urgent', label:'عاجلة',  color:'#dc2626', bg:'#fee2e2' },
]
const DEPARTMENTS = ['تقنية المعلومات','الشؤون المالية','الشؤون الإدارية','الموارد البشرية','إدارة المخاطر','التدقيق الداخلي','الرئاسة التنفيذية','التحول الرقمي']
const USERS = [
  { id:2, name:'أحمد الزهراني',  dept:'الشؤون المالية',      roleId:1 },
  { id:3, name:'مريم العنزي',    dept:'الشؤون الإدارية',  roleId:2 },
  { id:4, name:'خالد القحطاني', dept:'تقنية المعلومات',  roleId:2 },
  { id:5, name:'فاطمة الشمري',  dept:'الموارد البشرية',  roleId:3 },
  { id:6, name:'عمر الدوسري',   dept:'التدقيق الداخلي',  roleId:3 },
  { id:7, name:'نورة السبيعي',  dept:'الرئاسة التنفيذية',roleId:3 },
]

const MOCK_TASKS = [
  { id:1, title:'مراجعة عقود الموردين للربع الثاني', desc:'مراجعة وتدقيق جميع عقود الموردين المبرمة خلال الربع الثاني من عام 2026 والتحقق من مطابقتها للمعايير المعتمدة.', dept:'الشؤون المالية', assignedTo:2, assignedName:'أحمد الزهراني', priority:'high', status:'inprogress', due:'2026-04-30', created:'2026-04-01', createdBy:'مدير النظام', tags:['عقود','مالي'], comments:[{id:1,by:'أحمد الزهراني',text:'تم مراجعة 60% من العقود',date:'2026-04-15'}], attachments:[], escalated:false, history:[{status:'new',date:'2026-04-01'},{status:'assigned',date:'2026-04-02'},{status:'inprogress',date:'2026-04-05'}] },
  { id:2, title:'إعداد تقرير الأداء الشهري أبريل',   desc:'إعداد تقرير مفصل عن مؤشرات الأداء الرئيسية لشهر أبريل 2026 وتقديمه للإدارة العليا.',                               dept:'الشؤون الإدارية', assignedTo:3, assignedName:'مريم العنزي',  priority:'medium',status:'review',      due:'2026-04-25', created:'2026-04-10', createdBy:'مدير النظام', tags:['تقارير'],   comments:[], attachments:[], escalated:false, history:[{status:'new',date:'2026-04-10'},{status:'assigned',date:'2026-04-11'},{status:'review',date:'2026-04-20'}] },
  { id:3, title:'تحديث سياسة أمن المعلومات',          desc:'مراجعة وتحديث سياسة أمن المعلومات المؤسسية لتتوافق مع أحدث المعايير الدولية ISO 27001.',                                dept:'تقنية المعلومات',  assignedTo:4, assignedName:'خالد القحطاني',priority:'urgent',status:'new',         due:'2026-05-01', created:'2026-04-20', createdBy:'مدير النظام', tags:['أمن','سياسات'], comments:[], attachments:[], escalated:false, history:[{status:'new',date:'2026-04-20'}] },
  { id:4, title:'إجراء جلسة تدريبية للموظفين الجدد',  desc:'تنظيم وإدارة جلسة تدريبية شاملة للموظفين الجدد المنضمين خلال الفترة الأخيرة.',                                        dept:'الموارد البشرية', assignedTo:5, assignedName:'فاطمة الشمري',priority:'medium',status:'completed',  due:'2026-04-15', created:'2026-04-01', createdBy:'مدير النظام', tags:['تدريب','HR'],   comments:[{id:2,by:'فاطمة الشمري',text:'تمت الجلسة بنجاح، 15 موظف حضروا',date:'2026-04-15'}], attachments:[], escalated:false, history:[{status:'new',date:'2026-04-01'},{status:'completed',date:'2026-04-15'}] },
  { id:5, title:'تدقيق منظومة المشتريات',             desc:'إجراء تدقيق شامل على إجراءات المشتريات للتحقق من الامتثال للسياسات المعتمدة.',                                          dept:'التدقيق الداخلي', assignedTo:6, assignedName:'عمر الدوسري',  priority:'high',  status:'overdue',   due:'2026-04-10', created:'2026-03-25', createdBy:'مدير النظام', tags:['تدقيق'],    comments:[], attachments:[], escalated:true,  history:[{status:'new',date:'2026-03-25'},{status:'assigned',date:'2026-03-26'}] },
]

const STATUS_MAP = Object.fromEntries(STATUSES.map(s=>[s.key,s]))
const PRIO_MAP   = Object.fromEntries(PRIORITIES.map(p=>[p.key,p]))

// ─── Task Card ────────────────────────────────────────────────────────────────
function TaskCard({ task, onClick, selected }) {
  const s = STATUS_MAP[task.status] || STATUS_MAP.new
  const p = PRIO_MAP[task.priority] || PRIO_MAP.medium
  const isOverdue = task.due && new Date(task.due) < new Date() && !['completed','cancelled'].includes(task.status)
  return (
    <div onClick={onClick} className={`bg-white rounded-2xl border-2 p-4 cursor-pointer transition-all active:scale-[0.98] ${selected?'border-blue-500 shadow-md':'border-gray-100 hover:border-gray-200'}`}>
      <div className="flex items-start justify-between gap-2 mb-2">
        <div className="flex items-center gap-1.5 flex-shrink-0">
          <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full`} style={{background:s.bg,color:s.color}}>
            {s.icon} {s.label}
          </span>
          {task.escalated && <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-red-100 text-red-700">🔺 مُصعَّدة</span>}
        </div>
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full" style={{background:p.bg,color:p.color}}>{p.label}</span>
      </div>
      <p className="font-bold text-gray-900 text-sm leading-snug mb-2 line-clamp-2">{task.title}</p>
      <div className="flex items-center justify-between text-xs text-gray-400">
        <span>👤 {task.assignedName||'غير مُسنَدة'}</span>
        <span className={isOverdue?'text-red-500 font-semibold':''}>{isOverdue?'⚠️ ':'📅 '}{task.due}</span>
      </div>
      {task.comments?.length>0&&<div className="mt-2 text-[10px] text-gray-400">💬 {task.comments.length} تعليق</div>}
    </div>
  )
}

// ─── Create/Edit Task Modal ───────────────────────────────────────────────────
function TaskFormModal({ task, onClose, onSave }) {
  const isEdit = !!task?.id
  const [form, setForm] = useState({
    title: task?.title||'', desc: task?.desc||'',
    dept: task?.dept||'', assignedTo: task?.assignedTo||'',
    priority: task?.priority||'medium', due: task?.due||'',
    tags: (task?.tags||[]).join('، '),
  })
  const [errors, setErrors] = useState({})
  const set = (k,v) => { setForm(p=>({...p,[k]:v})); setErrors(p=>({...p,[k]:''})) }

  const validate = () => {
    const e = {}
    if (!form.title.trim()) e.title = 'العنوان مطلوب'
    if (!form.dept)         e.dept  = 'القسم مطلوب'
    if (!form.due)          e.due   = 'تاريخ الاستحقاق مطلوب'
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const handleSave = () => {
    if (!validate()) return
    const assignedUser = USERS.find(u=>u.id===Number(form.assignedTo))
    onSave({
      ...task,
      ...form,
      assignedTo: Number(form.assignedTo)||null,
      assignedName: assignedUser?.name||'',
      tags: form.tags.split('،').map(t=>t.trim()).filter(Boolean),
      status: task?.status||'new',
      created: task?.created||new Date().toISOString().split('T')[0],
      createdBy: task?.createdBy||'أنت',
      comments: task?.comments||[],
      attachments: task?.attachments||[],
      escalated: task?.escalated||false,
      history: task?.history||[{status:'new',date:new Date().toISOString().split('T')[0]}],
    })
    onClose()
  }

  const F = ({label,req,error,children}) => (
    <div>
      <label className="block text-xs font-bold text-gray-700 mb-1">{label}{req&&<span className="text-red-400 mr-1">*</span>}</label>
      {children}
      {error&&<p className="text-red-500 text-[10px] mt-0.5">{error}</p>}
    </div>
  )
  const INPUT_CLS = (err) => `w-full border rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right ${err?'border-red-300':'border-gray-200'}`

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4 overflow-y-auto" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-xl my-4" onClick={e=>e.stopPropagation()}>
        <div className="p-5 border-b border-gray-100 flex items-center justify-between">
          <h2 className="font-black text-gray-900">{isEdit?'تعديل المهمة':'إنشاء مهمة جديدة'}</h2>
          <button onClick={onClose} className="w-8 h-8 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
        </div>
        <div className="p-5 space-y-4">
          <F label="عنوان المهمة" req error={errors.title}>
            <input value={form.title} onChange={e=>set('title',e.target.value)} className={INPUT_CLS(errors.title)} placeholder="عنوان واضح وموجز..."/>
          </F>
          <F label="الوصف التفصيلي">
            <textarea value={form.desc} onChange={e=>set('desc',e.target.value)} rows={3} className={`${INPUT_CLS()} resize-none`} placeholder="وصف تفصيلي للمهمة والنتائج المطلوبة..."/>
          </F>
          <div className="grid grid-cols-2 gap-3">
            <F label="القسم المعني" req error={errors.dept}>
              <select value={form.dept} onChange={e=>set('dept',e.target.value)} className={INPUT_CLS(errors.dept)}>
                <option value="">— اختر القسم —</option>
                {DEPARTMENTS.map(d=><option key={d}>{d}</option>)}
              </select>
            </F>
            <F label="تكليف إلى">
              <select value={form.assignedTo} onChange={e=>set('assignedTo',e.target.value)} className={INPUT_CLS()}>
                <option value="">— اختر الموظف —</option>
                {USERS.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}
              </select>
            </F>
            <F label="الأولوية">
              <select value={form.priority} onChange={e=>set('priority',e.target.value)} className={INPUT_CLS()}>
                {PRIORITIES.map(p=><option key={p.key} value={p.key}>{p.label}</option>)}
              </select>
            </F>
            <F label="تاريخ الاستحقاق" req error={errors.due}>
              <input type="date" value={form.due} onChange={e=>set('due',e.target.value)} className={INPUT_CLS(errors.due)} min={new Date().toISOString().split('T')[0]}/>
            </F>
          </div>
          <F label="الوسوم (مفصولة بفاصلة)">
            <input value={form.tags} onChange={e=>set('tags',e.target.value)} className={INPUT_CLS()} placeholder="تدريب، مالي، عاجل، ..."/>
          </F>
        </div>
        <div className="p-5 border-t border-gray-100 flex gap-3">
          <button onClick={onClose} className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50">إلغاء</button>
          <button onClick={handleSave} className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 transition-colors">
            {isEdit?'💾 حفظ التعديلات':'✅ إنشاء المهمة'}
          </button>
        </div>
      </div>
    </div>
  )
}


// ─── Escalation Modal (hierarchy-aware) ────────────────────────────────────────
const ROLE_HIERARCHY = [
  { id:0, nameAr:'مشاهد',      code:'Viewer',            level:0, canEscalate:false },
  { id:1, nameAr:'موظف',       code:'Employee',          level:1, canEscalate:true,  escalatesTo:[2] },
  { id:2, nameAr:'مشرف',       code:'Supervisor',        level:2, canEscalate:true,  escalatesTo:[3] },
  { id:3, nameAr:'مدير القسم', code:'DepartmentManager', level:3, canEscalate:true,  escalatesTo:[3] },
  { id:4, nameAr:'مدير النظام',code:'SystemAdmin',       level:4, canEscalate:false },
]

function EscalationModal({ task, userRole, onClose, onEscalate }) {
  const role = ROLE_HIERARCHY.find(r => r.id === userRole) || ROLE_HIERARCHY[1]
  const allowedTargetRoles = role.escalatesTo || []
  const allowedUsers = USERS.filter(u => allowedTargetRoles.includes(u.roleId||2))

  const [targetUser, setTargetUser] = useState('')
  const [reason, setReason]         = useState('')
  const [error, setError]           = useState('')
  const [loading, setLoading]       = useState(false)

  const validate = () => {
    const target = USERS.find(u=>u.id===Number(targetUser))
    if (!targetUser || !target) { setError('يجب تحديد الشخص المُصعَّد إليه'); return false }
    if (!reason.trim())         { setError('يجب ذكر سبب التصعيد'); return false }
    // Hierarchy check
    const targetRole = target.roleId || 2
    if (!allowedTargetRoles.includes(targetRole)) {
      setError(role.id===1 && targetRole===3
        ? 'الموظف لا يستطيع التصعيد مباشرة لمدير القسم — يجب المرور عبر المشرف أولاً.'
        : 'لا يمكنك التصعيد لهذا الدور حسب صلاحياتك.')
      return false
    }
    return true
  }

  const handleSubmit = async () => {
    if (!validate()) return
    setLoading(true)
    const target = USERS.find(u=>u.id===Number(targetUser))
    try {
      await client.post('/api/v1/escalations', {
        taskId: task.id, toUserId: target.id,
        fromRole: role.id, toRole: target.roleId||2,
        reason, fromDepartment: task.dept,
      })
    } catch {}
    // Audit log entry (local)
    const escalationEntry = {
      id: Date.now(), taskId: task.id,
      fromUser: 'أنت', toUser: target.name,
      fromRole: role.nameAr, toRole: ROLE_HIERARCHY[target.roleId||2]?.nameAr,
      level: role.id===1 ? 1 : role.id===2 ? 2 : 3,
      reason, status: 'pending',
      date: new Date().toISOString().split('T')[0],
    }
    setLoading(false)
    onEscalate(escalationEntry)
    onClose()
  }

  const levelLabel = role.id===1 ? 'المستوى الأول: إلى المشرف' :
                     role.id===2 ? 'المستوى الثاني: إلى مدير القسم' :
                     'المستوى الثالث: تصعيد متقاطع'

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-[60] p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md" onClick={e=>e.stopPropagation()}>
        <div className="p-5 border-b border-gray-100">
          <div className="flex items-center gap-3 mb-3">
            <div className="w-10 h-10 bg-red-50 rounded-xl flex items-center justify-center text-2xl">🔺</div>
            <div>
              <h2 className="font-black text-gray-900">تصعيد المهمة</h2>
              <p className="text-xs text-gray-400">دورك: {role.nameAr}</p>
            </div>
            <button onClick={onClose} className="mr-auto w-8 h-8 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
          </div>

          {/* Hierarchy diagram */}
          <div className="bg-gray-50 rounded-xl p-3 flex items-center gap-2 text-xs">
            {ROLE_HIERARCHY.filter(r=>r.canEscalate).map((r,i,arr)=>(
              <React.Fragment key={r.id}>
                <span className={`px-2 py-1 rounded-lg font-bold ${r.id===role.id?'bg-blue-700 text-white':'bg-gray-200 text-gray-500'}`}>
                  {r.nameAr}
                </span>
                {i<arr.length-1&&<span className="text-gray-300">→</span>}
              </React.Fragment>
            ))}
          </div>
          <p className="text-xs text-blue-600 font-semibold mt-2">📍 {levelLabel}</p>
        </div>

        <div className="p-5 space-y-4">
          {/* Task info */}
          <div className="bg-amber-50 border border-amber-200 rounded-xl p-3">
            <p className="text-xs font-bold text-amber-800 mb-1">المهمة المُصعَّدة:</p>
            <p className="text-sm text-gray-800 font-semibold">{task.title}</p>
            <p className="text-xs text-gray-500 mt-0.5">القسم: {task.dept}</p>
          </div>

          {/* Target user */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">
              التصعيد إلى <span className="text-red-400">*</span>
            </label>
            {allowedUsers.length > 0 ? (
              <select value={targetUser} onChange={e=>{ setTargetUser(e.target.value); setError('') }}
                className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-red-400">
                <option value="">— اختر الشخص المسؤول —</option>
                {allowedUsers.map(u=>(
                  <option key={u.id} value={u.id}>{u.name} ({ROLE_HIERARCHY[u.roleId||2]?.nameAr})</option>
                ))}
              </select>
            ) : (
              <div className="bg-red-50 border border-red-200 rounded-xl p-3">
                <p className="text-sm text-red-700 font-semibold">⚠️ لا يوجد مستهدف مناسب لمستوى تصعيدك</p>
                <p className="text-xs text-red-500 mt-1">{role.canEscalate ? 'لا توجد جهات في النطاق المسموح' : 'دورك لا يُخوّلك بالتصعيد'}</p>
              </div>
            )}
          </div>

          {/* Reason */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">
              سبب التصعيد <span className="text-red-400">*</span>
            </label>
            <textarea value={reason} onChange={e=>{ setReason(e.target.value); setError('') }}
              rows={3} className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-400 text-right resize-none"
              placeholder="اشرح بوضوح لماذا تحتاج لتصعيد هذه المهمة وما الذي يحول دون حلها على مستواك..."/>
          </div>

          {/* Rules note */}
          <div className="bg-blue-50 border border-blue-100 rounded-xl p-3 text-xs text-blue-700 space-y-1">
            <p className="font-bold">⚠️ قواعد التصعيد:</p>
            <p>• التصعيد لا يُغير حالة المهمة تلقائياً</p>
            <p>• سيُسجَّل في سجل التدقيق</p>
            <p>• الطرف المُصعَّد إليه سيتلقى إشعاراً فورياً</p>
          </div>

          {error && <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700 font-medium">⚠️ {error}</div>}
        </div>

        <div className="p-5 border-t border-gray-100 flex gap-3">
          <button onClick={onClose} className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50">إلغاء</button>
          <button onClick={handleSubmit} disabled={loading || !role.canEscalate}
            className="flex-1 bg-red-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-red-700 disabled:opacity-50 transition-colors">
            {loading ? '⏳' : '🔺'} {loading ? 'جارٍ التصعيد...' : 'تأكيد التصعيد'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Task Detail Panel ────────────────────────────────────────────────────────
function TaskDetail({ task, onClose, onUpdate, isAdmin }) {
  const [comment, setComment] = useState('')
  const [files, setFiles]     = useState([])
  const [addingComment, setAddingComment] = useState(false)
  const [showEscModal, setShowEscModal]   = useState(false)
  const fileRef = useRef()
  const s = STATUS_MAP[task.status] || STATUS_MAP.new
  const p = PRIO_MAP[task.priority] || PRIO_MAP.medium
  const isOverdue = task.due && new Date(task.due) < new Date() && !['completed','cancelled'].includes(task.status)

  const nextStatuses = {
    new:        ['assigned'],
    assigned:   ['inprogress','cancelled'],
    inprogress: ['review','cancelled'],
    review:     ['completed','inprogress'],
    overdue:    ['inprogress','cancelled'],
    completed:  [],
    cancelled:  [],
  }

  const addComment = () => {
    if (!comment.trim()) return
    const newComment = { id:Date.now(), by:'أنت', text:comment.trim(), date:new Date().toISOString().split('T')[0] }
    onUpdate({ ...task, comments:[...task.comments, newComment] })
    setComment('')
    setAddingComment(false)
  }

  const changeStatus = (newStatus) => {
    onUpdate({
      ...task, status: newStatus,
      history: [...task.history, {status:newStatus, date:new Date().toISOString().split('T')[0]}]
    })
  }

  const escalate = () => {
    onUpdate({ ...task, escalated: true, history:[...task.history, {status:'escalated', date:new Date().toISOString().split('T')[0]}] })
  }

  const addFiles = (e) => {
    const newFiles = Array.from(e.target.files||[]).map(f=>({name:f.name, size:(f.size/1024).toFixed(0)+'KB', date:new Date().toISOString().split('T')[0]}))
    onUpdate({...task, attachments:[...(task.attachments||[]), ...newFiles]})
  }

  const nextBtns = nextStatuses[task.status] || []
  const STATUS_ACTIONS = {
    assigned:   { label:'بدء التنفيذ',      icon:'▶️', cls:'bg-blue-700 text-white hover:bg-blue-800' },
    inprogress: { label:'إرسال للمراجعة',   icon:'🔍', cls:'bg-purple-600 text-white hover:bg-purple-700' },
    review:     { label:'إغلاق ✅',          icon:'✅', cls:'bg-green-600 text-white hover:bg-green-700' },
    cancelled:  { label:'إلغاء المهمة',     icon:'⛔', cls:'bg-gray-500 text-white hover:bg-gray-600' },
  }

  return (
    <div className="fixed inset-0 z-40 md:relative md:inset-auto bg-white border-r border-gray-100 flex flex-col" style={{width:'100%',maxWidth:'none',height:'100%'}}>
          <style>{`.md-detail-panel { position: relative !important; width: 420px !important; }`}</style>
      {/* Header */}
      <div className="p-4 border-b border-gray-100 flex-shrink-0" style={{background: s.bg}}>
        <div className="flex items-start justify-between mb-2">
          <div className="flex gap-1.5 flex-wrap">
            <span className="text-xs font-bold px-2.5 py-1 rounded-full" style={{background:'white',color:s.color}}>{s.icon} {s.label}</span>
            <span className="text-xs font-bold px-2.5 py-1 rounded-full" style={{background:p.bg,color:p.color}}>{p.label}</span>
            {task.escalated && <span className="text-xs font-bold px-2.5 py-1 rounded-full bg-red-100 text-red-700">🔺 مُصعَّدة</span>}
          </div>
          <button onClick={onClose} className="w-9 h-9 rounded-xl bg-gray-100 hover:bg-gray-200 flex items-center justify-center text-gray-600 text-lg flex-shrink-0">✕</button>
        </div>
        <h2 className="font-black text-gray-900 leading-snug">{task.title}</h2>
        <div className="flex items-center gap-3 mt-2 text-xs text-gray-500 flex-wrap">
          {task.assignedName && <span>👤 {task.assignedName}</span>}
          <span>🏛 {task.dept}</span>
          <span className={isOverdue?'text-red-600 font-semibold':''}>{isOverdue?'⚠️ متأخرة: ':'📅 '}{task.due}</span>
        </div>
      </div>

      {/* Scrollable body */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* Description */}
        {task.desc && (
          <div>
            <p className="text-xs font-black text-gray-400 uppercase mb-1.5">الوصف</p>
            <p className="text-sm text-gray-700 leading-relaxed bg-gray-50 rounded-xl p-3">{task.desc}</p>
          </div>
        )}

        {/* Tags */}
        {task.tags?.length>0 && (
          <div className="flex flex-wrap gap-1.5">
            {task.tags.map(t=><span key={t} className="bg-blue-50 text-blue-600 text-[10px] px-2 py-0.5 rounded-full border border-blue-100">#{t}</span>)}
          </div>
        )}

        {/* Actions */}
        {(nextBtns.length > 0 || !task.escalated) && (
          <div className="space-y-2">
            <p className="text-xs font-black text-gray-400 uppercase">الإجراءات</p>
            <div className="flex flex-wrap gap-2">
              {nextBtns.map(next => {
                const a = STATUS_ACTIONS[next]
                if (!a) return null
                return (
                  <button key={next} onClick={()=>changeStatus(next)}
                    className={`text-xs font-bold px-3 py-2 rounded-xl transition-colors ${a.cls}`}>
                    {a.icon} {a.label}
                  </button>
                )
              })}
              {!task.escalated && !['completed','cancelled'].includes(task.status) && (
                <button onClick={()=>setShowEscModal(true)}
                  className="text-xs font-bold px-3 py-2 rounded-xl border-2 border-red-200 text-red-600 hover:bg-red-50 transition-colors">
                  🔺 تصعيد (مستوى {isAdmin?'المدير':'الموظف'})
                </button>
              )}
            </div>
          </div>
        )}

        {/* History */}
        <div>
          <p className="text-xs font-black text-gray-400 uppercase mb-2">سجل الحالات</p>
          <div className="space-y-1.5 border-r-2 border-gray-100 pr-3">
            {[...task.history].reverse().map((h,i)=>{
              const hs = STATUS_MAP[h.status]
              return (
                <div key={i} className="flex items-center gap-2 text-xs">
                  <div className={`w-2 h-2 rounded-full flex-shrink-0 -mr-4`} style={{background:hs?.dot||'#9ca3af'}}/>
                  <span className="font-medium" style={{color:hs?.color||'#6b7280'}}>{hs?.label||h.status}</span>
                  <span className="text-gray-400">{h.date}</span>
                </div>
              )
            })}
          </div>
        </div>

        {/* Attachments */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <p className="text-xs font-black text-gray-400 uppercase">المرفقات ({task.attachments?.length||0})</p>
            <button onClick={()=>fileRef.current?.click()} className="text-xs text-blue-600 hover:text-blue-800 font-semibold">+ إضافة</button>
          </div>
          <input ref={fileRef} type="file" multiple className="hidden" onChange={addFiles}/>
          {task.attachments?.length>0 ? (
            <div className="space-y-1.5">
              {task.attachments.map((a,i)=>(
                <div key={i} className="flex items-center gap-2 p-2 bg-gray-50 rounded-xl text-xs">
                  <span>📎</span>
                  <span className="flex-1 font-medium truncate text-gray-700">{a.name}</span>
                  <span className="text-gray-400">{a.size}</span>
                </div>
              ))}
            </div>
          ) : <p className="text-xs text-gray-400 text-center py-2">لا توجد مرفقات</p>}
        </div>

        {/* Comments */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <p className="text-xs font-black text-gray-400 uppercase">التعليقات ({task.comments?.length||0})</p>
            <button onClick={()=>setAddingComment(p=>!p)} className="text-xs text-blue-600 hover:text-blue-800 font-semibold">+ تعليق</button>
          </div>
          {addingComment && (
            <div className="mb-3 space-y-2">
              <textarea value={comment} onChange={e=>setComment(e.target.value)} rows={2}
                placeholder="اكتب تعليقك..."
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right resize-none"/>
              <div className="flex gap-2">
                <button onClick={addComment} className="flex-1 bg-blue-700 text-white text-xs py-2 rounded-xl font-bold hover:bg-blue-800">إضافة</button>
                <button onClick={()=>{setAddingComment(false);setComment('')}} className="border border-gray-200 text-gray-500 text-xs px-3 py-2 rounded-xl hover:bg-gray-50">إلغاء</button>
              </div>
            </div>
          )}
          {task.comments?.length>0 ? (
            <div className="space-y-2">
              {task.comments.map(c=>(
                <div key={c.id} className="bg-gray-50 rounded-xl p-3">
                  <div className="flex items-center justify-between text-[10px] text-gray-400 mb-1">
                    <span className="font-bold text-gray-600">👤 {c.by}</span>
                    <span>{c.date}</span>
                  </div>
                  <p className="text-xs text-gray-700">{c.text}</p>
                </div>
              ))}
            </div>
          ) : !addingComment && <p className="text-xs text-gray-400 text-center py-2">لا توجد تعليقات</p>}
        </div>

        {showEscModal && (
          <EscalationModal
            task={task}
            userRole={isAdmin ? 3 : 1}
            onClose={()=>setShowEscModal(false)}
            onEscalate={(esc) => {
              onUpdate({ ...task, escalated: true,
                escalations: [...(task.escalations||[]), esc],
                history: [...task.history, {status:'escalated', date:new Date().toISOString().split('T')[0]}]
              })
            }}
          />
        )}

        {/* Escalation history */}
        {task.escalations?.length > 0 && (
          <div>
            <p className="text-xs font-black text-gray-400 uppercase mb-2">سجل التصعيد</p>
            <div className="space-y-2">
              {task.escalations.map((e,i)=>(
                <div key={i} className="bg-red-50 border border-red-100 rounded-xl p-3 text-xs">
                  <div className="flex items-center justify-between mb-1">
                    <span className="font-bold text-red-700">🔺 المستوى {e.level}</span>
                    <span className={`px-2 py-0.5 rounded-full font-medium ${e.status==='pending'?'bg-amber-100 text-amber-700':'bg-green-100 text-green-700'}`}>
                      {e.status==='pending'?'معلق':e.status==='accepted'?'مقبول':'محلول'}
                    </span>
                  </div>
                  <p className="text-gray-600">{e.fromUser} ({e.fromRole}) → {e.toUser} ({e.toRole})</p>
                  <p className="text-gray-500 mt-0.5">السبب: {e.reason}</p>
                  <p className="text-gray-400 mt-0.5">{e.date}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Info */}
        <div className="text-xs text-gray-400 space-y-1 pt-2 border-t border-gray-100">
          <div className="flex justify-between"><span>أُنشئت بتاريخ</span><span>{task.created}</span></div>
          <div className="flex justify-between"><span>أنشأها</span><span>{task.createdBy}</span></div>
          {task.assignedName&&<div className="flex justify-between"><span>مُكلَّف</span><span>{task.assignedName}</span></div>}
        </div>
      </div>
    </div>
  )
}

// ─── Main Page ─────────────────────────────────────────────────────────────────
export default function TasksPage() {
  const { show, ToastContainer }     = useToast()
  const { user }                     = useAuthStore()
  const [tasks, setTasks]            = useLocalStorage('ecm_tasks_v2', MOCK_TASKS)
  const [selected, setSelected]      = useState(null)
  const [showCreate, setShowCreate]  = useState(false)
  const [editTask, setEditTask]      = useState(null)
  const [filterStatus, setFS]        = useState('all')
  const [filterPrio, setFP]          = useState('all')
  const [filterDept, setFD]          = useState('all')
  const [search, setSearch]          = useState('')
  const [view, setView]              = useState('board')  // board | list
  const [showReports, setShowReports]= useState(false)
  const [showFilters, setShowFilters]  = useState(false)

  const isAdmin = (user?.permissions||[]).some(p=>p==='admin.*')
  const safeTasks = Array.isArray(tasks) ? tasks : MOCK_TASKS

  // Mark overdue
  const withOverdue = safeTasks.map(t=>({
    ...t,
    status: !['completed','cancelled'].includes(t.status) && t.due && new Date(t.due) < new Date()
      ? 'overdue' : t.status
  }))

  const filtered = withOverdue.filter(t=>
    (filterStatus==='all'||t.status===filterStatus)&&
    (filterPrio==='all'||t.priority===filterPrio)&&
    (filterDept==='all'||t.dept===filterDept)&&
    (!search||(t.title||'').includes(search)||(t.assignedName||'').includes(search)||(t.dept||'').includes(search))
  )

  const updateTask = (updated) => {
    setTasks(p=>(Array.isArray(p)?p:MOCK_TASKS).map(t=>t.id===updated.id?updated:t))
    if (selected?.id===updated.id) setSelected(updated)
    client.post(`/api/v1/tasks/${updated.id}/assign`,{}).catch(()=>{})
  }

  const saveTask = (task) => {
    if (task.id) {
      updateTask(task)
      show(`✅ تم تعديل: ${task.title}`, 'success')
    } else {
      const newTask = { ...task, id: Date.now() }
      setTasks(p=>[newTask,...(Array.isArray(p)?p:MOCK_TASKS)])
      show(`✅ تم إنشاء: ${task.title}`, 'success')
      client.post('/api/v1/tasks', newTask).catch(()=>{})
    }
  }

  const deleteTask = (id) => {
    const t = safeTasks.find(t=>t.id===id)
    if (t?.status==='completed') { show('لا يمكن حذف مهمة مكتملة','error'); return }
    setTasks(p=>(Array.isArray(p)?p:[]).filter(t=>t.id!==id))
    setSelected(null)
    show('تم حذف المهمة','success')
  }

  // Board columns
  const boardCols = STATUSES.filter(s=>!['overdue','cancelled'].includes(s.key)).map(s=>({
    ...s,
    tasks: filtered.filter(t=>t.status===s.key)
  }))
  const overdueTasks   = filtered.filter(t=>t.status==='overdue')
  const cancelledTasks = filtered.filter(t=>t.status==='cancelled')

  // Stats
  const stats = {
    total:     safeTasks.length,
    inprogress:withOverdue.filter(t=>t.status==='inprogress').length,
    overdue:   withOverdue.filter(t=>t.status==='overdue').length,
    completed: withOverdue.filter(t=>t.status==='completed').length,
    escalated: withOverdue.filter(t=>t.escalated).length,
    byDept:    DEPARTMENTS.map(d=>({name:d, count:withOverdue.filter(t=>t.dept===d).length})).filter(d=>d.count>0),
    byStatus:  STATUSES.map(s=>({...s, count:withOverdue.filter(t=>t.status===s.key).length})),
  }

  return (
    <div className="flex flex-col h-full">
      <ToastContainer/>
      {(showCreate||editTask) && (
        <TaskFormModal task={editTask} onClose={()=>{setShowCreate(false);setEditTask(null)}} onSave={saveTask}/>
      )}

      {/* ── Header ── */}
      <div className="flex-shrink-0 space-y-3 mb-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-black text-gray-900">إدارة المهام</h1>
            <p className="text-sm text-gray-400 mt-0.5">{safeTasks.length} مهمة · {stats.overdue} متأخرة · {stats.escalated} مُصعَّدة</p>
          </div>
          <div className="flex gap-2">
            <button onClick={()=>setShowReports(p=>!p)}
              className={`border text-sm px-4 py-2 rounded-xl transition-colors ${showReports?'bg-gray-900 text-white border-gray-900':'border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
              📊 التقارير
            </button>
            <button onClick={()=>{setShowCreate(true);setEditTask(null)}}
              className="bg-blue-700 text-white px-5 py-2 rounded-xl text-sm font-bold hover:bg-blue-800 shadow-sm transition-colors">
              + مهمة جديدة
            </button>
          </div>
        </div>

        {/* KPI */}
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2 md:gap-3">
          {[
            {l:'إجمالي المهام',   v:stats.total,     icon:'📋', cls:'bg-indigo-50 text-indigo-700 border-indigo-100'},
            {l:'قيد التنفيذ',    v:stats.inprogress, icon:'🔄', cls:'bg-amber-50 text-amber-700 border-amber-100'},
            {l:'متأخرة',          v:stats.overdue,   icon:'⚠️', cls:'bg-red-50 text-red-700 border-red-100'},
            {l:'مكتملة',          v:stats.completed, icon:'✅', cls:'bg-green-50 text-green-700 border-green-100'},
            {l:'مُصعَّدة',        v:stats.escalated, icon:'🔺', cls:'bg-rose-50 text-rose-700 border-rose-100'},
          ].map(k=>(
            <div key={k.l} className={`${k.cls} border rounded-2xl p-3 flex items-center gap-2.5`}>
              <span className="text-2xl">{k.icon}</span>
              <div><p className="text-xl font-black">{k.v}</p><p className="text-[10px] opacity-80">{k.l}</p></div>
            </div>
          ))}
        </div>

        {/* Reports panel */}
        {showReports && (
          <div className="bg-white rounded-2xl border border-gray-100 p-5 shadow-sm">
            <p className="font-bold text-gray-800 mb-4">📊 تقارير المهام</p>
            <div className="grid grid-cols-2 gap-6">
              <div>
                <p className="text-xs font-black text-gray-500 mb-2 uppercase">حسب الحالة</p>
                {stats.byStatus.map(s=>(
                  <div key={s.key} className="flex items-center gap-2 mb-2">
                    <div className="w-2 h-2 rounded-full flex-shrink-0" style={{background:s.dot}}/>
                    <span className="text-xs text-gray-600 flex-1">{s.label}</span>
                    <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div className="h-full rounded-full transition-all" style={{width:`${stats.total?s.count/stats.total*100:0}%`,background:s.color}}/>
                    </div>
                    <span className="text-xs font-bold text-gray-700 w-6 text-left">{s.count}</span>
                  </div>
                ))}
              </div>
              <div>
                <p className="text-xs font-black text-gray-500 mb-2 uppercase">حسب القسم</p>
                {stats.byDept.slice(0,6).map(d=>(
                  <div key={d.name} className="flex items-center gap-2 mb-2">
                    <span className="text-xs text-gray-600 flex-1 truncate">{d.name}</span>
                    <div className="w-20 h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div className="h-full bg-blue-400 rounded-full" style={{width:`${stats.total?d.count/stats.total*100:0}%`}}/>
                    </div>
                    <span className="text-xs font-bold text-gray-700 w-4 text-left">{d.count}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Filters - mobile toggle */}
        <div>
          <button onClick={()=>setShowFilters(p=>!p)} className="md:hidden w-full flex items-center justify-between bg-white border border-gray-200 rounded-xl px-4 py-2.5 text-sm font-medium text-gray-700 mb-2">
            <span>🔍 البحث والتصفية</span>
            <span>{showFilters?'▲':'▼'}</span>
          </button>
        <div className={`bg-white rounded-2xl border border-gray-100 p-3 flex gap-2 flex-wrap ${showFilters?'flex':'hidden md:flex'}`}>
          <div className="relative flex-1 min-w-36">
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
            <input value={search} onChange={e=>setSearch(e.target.value)} placeholder="بحث..."
              className="w-full pr-8 pl-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
          </div>
          <select value={filterStatus} onChange={e=>setFS(e.target.value)} className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
            <option value="all">كل الحالات</option>
            {STATUSES.map(s=><option key={s.key} value={s.key}>{s.label}</option>)}
          </select>
          <select value={filterPrio} onChange={e=>setFP(e.target.value)} className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
            <option value="all">كل الأولويات</option>
            {PRIORITIES.map(p=><option key={p.key} value={p.key}>{p.label}</option>)}
          </select>
          <select value={filterDept} onChange={e=>setFD(e.target.value)} className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
            <option value="all">كل الأقسام</option>
            {DEPARTMENTS.map(d=><option key={d}>{d}</option>)}
          </select>
          <div className="flex border border-gray-200 rounded-xl overflow-hidden">
            <button onClick={()=>setView('board')} className={`px-3 py-2 text-sm ${view==='board'?'bg-gray-900 text-white':'text-gray-500 hover:bg-gray-50'}`}>⊟ لوحة</button>
            <button onClick={()=>setView('list')}  className={`px-3 py-2 text-sm ${view==='list'?'bg-gray-900 text-white':'text-gray-500 hover:bg-gray-50'}`}>☰ قائمة</button>
          </div>
        </div>
        </div>
      </div>

      {/* ── Content ── */}
      <div className="flex-1 flex gap-4 overflow-hidden min-h-0">

        {/* Board view */}
        {view==='board' && (
          <div className="flex-1 overflow-x-auto overflow-y-hidden">
            <div className="flex gap-4 h-full pb-2" style={{minWidth: (boardCols.length+1)*240}}>
              {boardCols.map(col=>(
                <div key={col.key} className="flex-shrink-0 flex flex-col rounded-2xl overflow-hidden" style={{width:240,background:col.bg}}>
                  <div className="px-3 py-2.5 flex items-center gap-2 flex-shrink-0 border-b" style={{borderColor:col.color+'30'}}>
                    <span>{col.icon}</span>
                    <span className="text-xs font-black flex-1" style={{color:col.color}}>{col.label}</span>
                    <span className="text-xs font-bold px-1.5 py-0.5 rounded-full" style={{background:col.color,color:'white'}}>{col.tasks.length}</span>
                  </div>
                  <div className="flex-1 overflow-y-auto p-2 space-y-2">
                    {col.tasks.map(t=>(
                      <TaskCard key={t.id} task={t} selected={selected?.id===t.id} onClick={()=>setSelected(selected?.id===t.id?null:t)}/>
                    ))}
                    {col.tasks.length===0 && <div className="text-center py-6 text-gray-400 text-xs">لا توجد مهام</div>}
                  </div>
                </div>
              ))}
              {/* Overdue + Cancelled column */}
              {(overdueTasks.length>0||cancelledTasks.length>0) && (
                <div className="flex-shrink-0 flex flex-col gap-2" style={{width:240}}>
                  {overdueTasks.length>0 && (
                    <div className="flex-shrink-0 rounded-2xl overflow-hidden" style={{background:'#fef2f2'}}>
                      <div className="px-3 py-2 flex items-center gap-2 border-b border-red-100">
                        <span>⚠️</span><span className="text-xs font-black text-red-600 flex-1">متأخرة</span>
                        <span className="text-xs font-bold px-1.5 py-0.5 rounded-full bg-red-600 text-white">{overdueTasks.length}</span>
                      </div>
                      <div className="p-2 space-y-2 max-h-64 overflow-y-auto">
                        {overdueTasks.map(t=><TaskCard key={t.id} task={t} selected={selected?.id===t.id} onClick={()=>setSelected(selected?.id===t.id?null:t)}/>)}
                      </div>
                    </div>
                  )}
                  {cancelledTasks.length>0 && (
                    <div className="flex-shrink-0 rounded-2xl overflow-hidden" style={{background:'#f9fafb'}}>
                      <div className="px-3 py-2 flex items-center gap-2 border-b border-gray-200">
                        <span>⛔</span><span className="text-xs font-black text-gray-500 flex-1">ملغاة</span>
                        <span className="text-xs font-bold px-1.5 py-0.5 rounded-full bg-gray-500 text-white">{cancelledTasks.length}</span>
                      </div>
                      <div className="p-2 space-y-2 max-h-48 overflow-y-auto">
                        {cancelledTasks.map(t=><TaskCard key={t.id} task={t} selected={selected?.id===t.id} onClick={()=>setSelected(selected?.id===t.id?null:t)}/>)}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        {/* List view */}
        {view==='list' && (
          <div className="flex-1 overflow-y-auto">
            {filtered.length===0 ? (
              <div className="bg-white rounded-2xl border border-gray-100 p-16 text-center text-gray-400">
                <div className="text-5xl mb-3">📋</div><p>لا توجد مهام</p>
              </div>
            ) : (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
                <table className="w-full text-sm">
                  <thead><tr className="bg-gray-50 border-b border-gray-100">
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400">المهمة</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">القسم</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400">الحالة</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">الأولوية</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden lg:table-cell">الاستحقاق</th>
                    {isAdmin && <th className="px-4 py-3 text-right text-xs font-black text-gray-400">إجراء</th>}
                  </tr></thead>
                  <tbody className="divide-y divide-gray-50">
                    {filtered.map(t=>{
                      const s = STATUS_MAP[t.status]||STATUS_MAP.new
                      const p = PRIO_MAP[t.priority]||PRIO_MAP.medium
                      const over = t.due && new Date(t.due)<new Date() && !['completed','cancelled'].includes(t.status)
                      return (
                        <tr key={t.id} onClick={()=>setSelected(selected?.id===t.id?null:t)}
                          className={`cursor-pointer transition-colors ${selected?.id===t.id?'bg-blue-50':'hover:bg-gray-50'}`}>
                          <td className="px-4 py-3">
                            <p className="font-bold text-gray-900 truncate max-w-[200px]">{t.title}</p>
                            <p className="text-[10px] text-gray-400">{t.assignedName||'غير مُسنَدة'} {t.escalated&&'· 🔺'}</p>
                          </td>
                          <td className="px-4 py-3 hidden md:table-cell text-xs text-gray-500">{t.dept}</td>
                          <td className="px-4 py-3"><span className="text-[11px] px-2 py-0.5 rounded-full font-medium" style={{background:s.bg,color:s.color}}>{s.icon} {s.label}</span></td>
                          <td className="px-4 py-3 hidden md:table-cell"><span className="text-[11px] px-2 py-0.5 rounded-full font-medium" style={{background:p.bg,color:p.color}}>{p.label}</span></td>
                          <td className={`px-4 py-3 hidden lg:table-cell text-xs font-medium ${over?'text-red-600':''}`}>{over?'⚠️ ':''}{t.due}</td>
                          {isAdmin && <td className="px-4 py-3">
                            <div className="flex gap-2">
                              <button onClick={e=>{e.stopPropagation();setEditTask(t)}} className="text-xs text-blue-600 hover:underline">تعديل</button>
                              <button onClick={e=>{e.stopPropagation();deleteTask(t.id)}} className="text-xs text-red-500 hover:underline">حذف</button>
                            </div>
                          </td>}
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Detail panel */}
        {selected && (
          <TaskDetail
            task={selected}
            isAdmin={isAdmin}
            onClose={()=>setSelected(null)}
            onUpdate={updateTask}
          />
        )}
      </div>
    </div>
  )
}
