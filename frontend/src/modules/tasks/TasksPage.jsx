import { useUsers } from '../../hooks/useUsers'
import React, { useState, useRef } from 'react'
import { useLocalStorage } from '../../hooks/useLocalStorage'
import { useAuthStore } from '../../store/authStore'
import { useToast } from '../../components/Toast'
import { useLang } from '../../i18n.js'
import client from '../../api/client'

const STATUSES = [
  { key:'new',        labelAr:'جديدة',         labelEn:'New',           icon:'🆕', color:'#6366f1', bg:'#eef2ff', dot:'#818cf8' },
  { key:'assigned',   labelAr:'مُسنَدة',        labelEn:'Assigned',      icon:'👤', color:'#0369a1', bg:'#eff6ff', dot:'#38bdf8' },
  { key:'inprogress', labelAr:'قيد التنفيذ',   labelEn:'In Progress',   icon:'🔄', color:'#b45309', bg:'#fffbeb', dot:'#f59e0b' },
  { key:'review',     labelAr:'قيد المراجعة',  labelEn:'Under Review',  icon:'🔍', color:'#7c3aed', bg:'#f5f3ff', dot:'#a78bfa' },
  { key:'completed',  labelAr:'مكتملة',        labelEn:'Completed',     icon:'✅', color:'#059669', bg:'#ecfdf5', dot:'#34d399' },
  { key:'overdue',    labelAr:'متأخرة',         labelEn:'Overdue',       icon:'⚠️', color:'#dc2626', bg:'#fef2f2', dot:'#f87171' },
  { key:'cancelled',  labelAr:'ملغاة',          labelEn:'Cancelled',     icon:'⛔', color:'#6b7280', bg:'#f9fafb', dot:'#9ca3af' },
  { key:'returned',   labelAr:'مُرجَعة للتعديل', labelEn:'Returned',      icon:'↩️', color:'#be185d', bg:'#fdf2f8', dot:'#f472b6' },
]
const PRIORITIES = [
  { key:'low',    labelAr:'منخفضة', labelEn:'Low',    color:'#16a34a', bg:'#dcfce7' },
  { key:'medium', labelAr:'متوسطة', labelEn:'Medium', color:'#d97706', bg:'#fef9c3' },
  { key:'high',   labelAr:'عالية',  labelEn:'High',   color:'#ea580c', bg:'#ffedd5' },
  { key:'urgent', labelAr:'عاجلة',  labelEn:'Urgent', color:'#dc2626', bg:'#fee2e2' },
]
const DEPARTMENTS = ['تقنية المعلومات','الشؤون المالية','الشؤون الإدارية','الموارد البشرية','إدارة المخاطر','التدقيق الداخلي','الرئاسة التنفيذية','التحول الرقمي']
// USERS now from useUsers() hook — see main component
const ROLE_HIERARCHY = [
  { id:0, nameAr:'مشاهد',      nameEn:'Viewer',            level:0, canEscalate:false },
  { id:1, nameAr:'موظف',       nameEn:'Employee',          level:1, canEscalate:true,  escalatesTo:[2] },
  { id:2, nameAr:'مشرف',       nameEn:'Supervisor',        level:2, canEscalate:true,  escalatesTo:[3] },
  { id:3, nameAr:'مدير القسم', nameEn:'Dept. Manager',     level:3, canEscalate:true,  escalatesTo:[3] },
  { id:4, nameAr:'مدير النظام',nameEn:'System Admin',      level:4, canEscalate:false },
]
const MOCK_TASKS = []  // Start fresh — add real tasks
const STATUS_MAP = Object.fromEntries(STATUSES.map(s=>[s.key,s]))
const PRIO_MAP   = Object.fromEntries(PRIORITIES.map(p=>[p.key,p]))

// ─── Shared input class helper (module-level, never recreated) ─────────────────
const INP = (err) => `w-full border rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 ${err?'border-red-300':'border-gray-200'}`

// ─── Escalation Modal ─────────────────────────────────────────────────────────
function EscalationModal({ task, userRole, onClose, onEscalate, t, lang }) {
  const role = ROLE_HIERARCHY.find(r=>r.id===userRole)||ROLE_HIERARCHY[1]
  const allowed = role.escalatesTo||[]
  const targets = USERS.filter(u=>allowed.includes(u.roleId||2))
  const [targetUser,setTU] = useState('')
  const [reason,setR]      = useState('')
  const [error,setE]       = useState('')
  const [loading,setL]     = useState(false)
  const rn = (r) => lang==='en' ? (r.nameEn||r.nameAr) : r.nameAr
  const levelLabel = role.id===1?t('level1'):role.id===2?t('level2'):t('level3')

  const submit = async () => {
    if (!targetUser) { setE(t('choose_person')); return }
    if (!reason.trim()) { setE(t('esc_reason_label')+' '+t('required')); return }
    setL(true)
    const tgt = USERS.find(u=>u.id===Number(targetUser))
    try { await client.post('/api/v1/escalations',{taskId:task.id,toUserId:tgt.id,fromRole:role.id,toRole:tgt.roleId||2,reason,fromDepartment:task.dept}) } catch {}
    const entry = { id:Date.now(), taskId:task.id, fromUser:'أنت', toUser:tgt.name, fromRole:rn(role), toRole:ROLE_HIERARCHY[tgt.roleId||2]?rn(ROLE_HIERARCHY[tgt.roleId||2]):'', level:role.id===1?1:role.id===2?2:3, reason, status:'pending', date:new Date().toISOString().split('T')[0] }
    setL(false); onEscalate(entry); onClose()
  }

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-[60] p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md" onClick={e=>e.stopPropagation()}>
        <div className="p-5 border-b border-gray-100">
          <div className="flex items-center gap-3 mb-3">
            <div className="w-10 h-10 bg-red-50 rounded-xl flex items-center justify-center text-2xl">🔺</div>
            <div><h2 className="font-black text-gray-900">{t('escalate_task')}</h2><p className="text-xs text-gray-400">{t('your_role')}: {rn(role)}</p></div>
            <button onClick={onClose} className="mr-auto w-8 h-8 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
          </div>
          <div className="bg-gray-50 rounded-xl p-3 flex items-center gap-2 text-xs flex-wrap">
            {ROLE_HIERARCHY.filter(r=>r.canEscalate).map((r,i,arr)=>(
              <React.Fragment key={r.id}>
                <span className={`px-2 py-1 rounded-lg font-bold ${r.id===role.id?'bg-blue-700 text-white':'bg-gray-200 text-gray-500'}`}>{rn(r)}</span>
                {i<arr.length-1&&<span className="text-gray-300">→</span>}
              </React.Fragment>
            ))}
          </div>
          <p className="text-xs text-blue-600 font-semibold mt-2">📍 {levelLabel}</p>
        </div>
        <div className="p-5 space-y-4">
          <div className="bg-amber-50 border border-amber-200 rounded-xl p-3">
            <p className="text-xs font-bold text-amber-800 mb-1">{t('escalated_task_label')}</p>
            <p className="text-sm text-gray-800 font-semibold">{task.title}</p>
            <p className="text-xs text-gray-500 mt-0.5">{task.dept}</p>
          </div>
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">{t('escalate_to_label')} <span className="text-red-400">*</span></label>
            {targets.length>0?(
              <select value={targetUser} onChange={e=>{setTU(e.target.value);setE('')}} className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-red-400">
                <option value="">{t('choose_person')}</option>
                {targets.map(u=><option key={u.id} value={u.id}>{u.name} ({ROLE_HIERARCHY[u.roleId||2]?rn(ROLE_HIERARCHY[u.roleId||2]):''})</option>)}
              </select>
            ):(
              <div className="bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700">{t('no_valid_targets')}</div>
            )}
          </div>
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">{t('escalation_reason_label')} <span className="text-red-400">*</span></label>
            <textarea value={reason} onChange={e=>{setR(e.target.value);setE('')}} rows={3} className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-400 resize-none" placeholder={t('escalation_reason_ph')} dir={lang==='en'?'ltr':'rtl'}/>
          </div>
          <div className="bg-blue-50 border border-blue-100 rounded-xl p-3 text-xs text-blue-700 space-y-1">
            <p className="font-bold">⚠️ {t('escalation_rules')}</p>
            <p>• {t('esc_rule1')}</p><p>• {t('esc_rule2')}</p><p>• {t('esc_rule3')}</p>
          </div>
          {error&&<div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">⚠️ {error}</div>}
        </div>
        <div className="p-5 border-t border-gray-100 flex gap-3">
          <button onClick={onClose} className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50">{t('cancel')}</button>
          <button onClick={submit} disabled={loading||!role.canEscalate} className="flex-1 bg-red-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-red-700 disabled:opacity-50 transition-colors">
            {loading?t('escalating'):'🔺 '+t('confirm_escalation')}
          </button>
        </div>
      </div>
    </div>
  )
}


// ─── Assign Task Modal ────────────────────────────────────────────────────────
const ASSIGNMENT_ROLES = [
  { key:'execute',  labelAr:'تنفيذ',   labelEn:'Execute',  icon:'▶️',  color:'#2563eb', bg:'#eff6ff', descAr:'مسؤول عن تنفيذ المهمة كاملاً', descEn:'Responsible for full task execution' },
  { key:'review',   labelAr:'مراجعة',  labelEn:'Review',   icon:'🔍', color:'#7c3aed', bg:'#f5f3ff', descAr:'يراجع النتائج ويعطي ملاحظات',      descEn:'Reviews results and provides feedback' },
  { key:'edit',     labelAr:'تعديل',   labelEn:'Edit',     icon:'✏️',  color:'#d97706', bg:'#fffbeb', descAr:'يُعدّل المخرجات والمحتوى',           descEn:'Edits outputs and content' },
  { key:'approve',  labelAr:'اعتماد',  labelEn:'Approve',  icon:'✅', color:'#059669', bg:'#ecfdf5', descAr:'يعتمد المهمة ويُغلقها',              descEn:'Approves and closes the task' },
  { key:'consult',  labelAr:'استشارة', labelEn:'Consult',  icon:'💬', color:'#0891b2', bg:'#ecfeff', descAr:'يُستشار عند الحاجة فقط',             descEn:'Consulted when needed only' },
]

function AssignModal({ task, onClose, onAssign, t, lang }) {
  const [selectedUser, setUser]   = useState(task.assignedTo ? String(task.assignedTo) : '')
  const [assignRole,   setRole]   = useState('execute')
  const [note,         setNote]   = useState('')
  const [loading,      setL]      = useState(false)

  const rl = (r) => lang==='en' ? (r.labelEn||r.labelAr) : r.labelAr
  const rd = (r) => lang==='en' ? (r.descEn||r.descAr)  : r.descAr
  const selectedRole = ASSIGNMENT_ROLES.find(r=>r.key===assignRole)
  const selectedUserObj = USERS.find(u=>String(u.id)===selectedUser)

  const handleSubmit = async () => {
    if (!selectedUser) return
    setL(true)
    try { await client.post(`/api/v1/tasks/${task.id}/assign`, { toUserId: Number(selectedUser), role: assignRole }) } catch {}
    const entry = {
      id: Date.now(), by: 'أنت',
      text: `${lang==='en'?'Assigned to':'تم الإسناد إلى'} ${selectedUserObj?.name} ${lang==='en'?'for':'بدور'} ${rl(selectedRole)}${note?' — '+note:''}`,
      date: new Date().toISOString().split('T')[0], type: 'assign'
    }
    onAssign({
      ...task,
      assignedTo:   Number(selectedUser),
      assignedName: selectedUserObj?.name || '',
      assignRole:   assignRole,
      comments: [...(task.comments||[]), entry],
      history:  [...(task.history||[]),  { status: task.status, date: new Date().toISOString().split('T')[0], assignedTo: selectedUserObj?.name, assignRole: assignRole }],
    })
    setL(false); onClose()
  }

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-[60] p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg" onClick={e=>e.stopPropagation()}>
        {/* Header */}
        <div className="p-5 border-b border-gray-100 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-50 rounded-xl flex items-center justify-center text-xl">👤</div>
            <div>
              <h2 className="font-black text-gray-900">{lang==='en'?'Assign Task':'إسناد المهمة'}</h2>
              <p className="text-xs text-gray-400 mt-0.5">{lang==='en'?'Assign to any team member at any stage':'يمكن الإسناد في أي مرحلة'}</p>
            </div>
          </div>
          <button onClick={onClose} className="w-9 h-9 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
        </div>

        <div className="p-5 space-y-5">
          {/* Task reminder */}
          <div className="bg-gray-50 rounded-xl p-3 flex items-start gap-2">
            <span className="text-gray-400 text-sm mt-0.5">📋</span>
            <div>
              <p className="text-xs text-gray-500">{lang==='en'?'Task:':'المهمة:'}</p>
              <p className="text-sm font-bold text-gray-800">{task.title}</p>
            </div>
          </div>

          {/* Select user */}
          <div>
            <label className="block text-xs font-black text-gray-700 mb-2 uppercase tracking-wide">
              {lang==='en'?'Assign To':'إسناد إلى'} <span className="text-red-400">*</span>
            </label>
            <div className="grid grid-cols-1 gap-2 max-h-44 overflow-y-auto">
              {USERS.map(u => (
                <button key={u.id} onClick={()=>setUser(String(u.id))}
                  className={`flex items-center gap-3 p-3 rounded-xl border-2 text-right transition-all ${String(u.id)===selectedUser?'border-blue-500 bg-blue-50':'border-gray-100 hover:border-gray-200 hover:bg-gray-50'}`}>
                  <div className="w-9 h-9 rounded-full bg-blue-600 flex items-center justify-center text-white text-sm font-bold flex-shrink-0">
                    {u.name[0]}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="font-bold text-gray-900 text-sm">{u.name}</p>
                    <p className="text-xs text-gray-400">{u.dept}</p>
                  </div>
                  {String(u.id)===selectedUser && <span className="text-blue-500 text-lg flex-shrink-0">✓</span>}
                </button>
              ))}
            </div>
          </div>

          {/* Select assignment role */}
          <div>
            <label className="block text-xs font-black text-gray-700 mb-2 uppercase tracking-wide">
              {lang==='en'?'Assignment Role':'دور الإسناد'} <span className="text-red-400">*</span>
            </label>
            <div className="grid grid-cols-2 gap-2">
              {ASSIGNMENT_ROLES.map(r => (
                <button key={r.key} onClick={()=>setRole(r.key)}
                  className={`flex items-center gap-2.5 p-3 rounded-xl border-2 text-right transition-all ${assignRole===r.key?'shadow-sm':'border-gray-100 hover:border-gray-200'}`}
                  style={assignRole===r.key?{borderColor:r.color,background:r.bg}:{}}>
                  <span className="text-lg flex-shrink-0">{r.icon}</span>
                  <div className="min-w-0">
                    <p className="text-xs font-bold" style={{color:assignRole===r.key?r.color:'#374151'}}>{rl(r)}</p>
                    <p className="text-[10px] text-gray-400 leading-tight">{rd(r)}</p>
                  </div>
                </button>
              ))}
            </div>
          </div>

          {/* Optional note */}
          <div>
            <label className="block text-xs font-black text-gray-700 mb-1.5 uppercase tracking-wide">
              {lang==='en'?'Note (optional)':'ملاحظة (اختياري)'}
            </label>
            <textarea value={note} onChange={e=>setNote(e.target.value)} rows={2}
              placeholder={lang==='en'?'Add instructions or context...':'أضف تعليمات أو سياق للمهمة...'}
              className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 resize-none"
              dir={lang==='en'?'ltr':'rtl'}/>
          </div>

          {/* Preview */}
          {selectedUser && (
            <div className="rounded-xl p-3 border" style={{background:selectedRole?.bg,borderColor:selectedRole?.color+'40'}}>
              <p className="text-xs font-bold" style={{color:selectedRole?.color}}>
                {lang==='en'?'Summary:':'الملخص:'} {selectedUserObj?.name} ← {selectedRole?.icon} {rl(selectedRole)}
              </p>
            </div>
          )}
        </div>

        <div className="p-5 border-t border-gray-100 flex gap-3">
          <button onClick={onClose} className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50">
            {t('cancel')}
          </button>
          <button onClick={handleSubmit} disabled={!selectedUser||loading}
            className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-40 transition-colors">
            {loading?'...':`👤 ${lang==='en'?'Confirm Assignment':'تأكيد الإسناد'}`}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Task Card ─────────────────────────────────────────────────────────────────
function TaskCard({ task, onClick, selected, lang, onDragStart, onDragEnd }) {
  const s = STATUS_MAP[task.status]||STATUS_MAP.new
  const p = PRIO_MAP[task.priority]||PRIO_MAP.medium
  const sl = lang==='en'?(s.labelEn||s.labelAr):s.labelAr
  const pl = lang==='en'?(p.labelEn||p.labelAr):p.labelAr
  const isOverdue = task.due && new Date(task.due)<new Date() && !['completed','cancelled'].includes(task.status)
  return (
    <div
      onClick={onClick}
      draggable={!['completed','cancelled'].includes(task.status)}
      onDragStart={e => { e.dataTransfer.effectAllowed='move'; onDragStart&&onDragStart(task.id) }}
      onDragEnd={() => onDragEnd&&onDragEnd()}
      className={`bg-white rounded-2xl border-2 p-4 transition-all hover:shadow-md
        ${['completed','cancelled'].includes(task.status)?'cursor-default opacity-80':'cursor-grab active:cursor-grabbing'}
        ${selected?'border-blue-500 shadow-md':'border-gray-100 hover:border-gray-200'}`}>
      <div className="flex items-start justify-between gap-2 mb-2">
        <div className="flex items-center gap-1.5 flex-wrap">
          <span className="text-[10px] font-bold px-2 py-0.5 rounded-full" style={{background:s.bg,color:s.color}}>{s.icon} {sl}</span>
          {task.escalated&&<span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-red-100 text-red-700">🔺</span>}
        </div>
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full flex-shrink-0" style={{background:p.bg,color:p.color}}>{pl}</span>
      </div>
      <div className="flex items-start gap-1.5 mb-2">
        {!['completed','cancelled'].includes(task.status) && (
          <span className="text-gray-300 text-xs mt-0.5 flex-shrink-0 select-none">⠿⠿</span>
        )}
        <p className="font-bold text-gray-900 text-sm leading-snug line-clamp-2">{task.title}</p>
      </div>
      <div className="flex items-center justify-between text-xs text-gray-400">
        <span className="flex items-center gap-1">
          <span>👤 {task.assignedName||'—'}</span>
          {task.assignRole&&(()=>{const r=ASSIGNMENT_ROLES.find(ar=>ar.key===task.assignRole); return r?<span className="font-bold" style={{color:r.color}}>{r.icon}</span>:null})()}
        </span>
        <span className={isOverdue?'text-red-500 font-semibold':''}>{isOverdue?'⚠️ ':''}{task.due}</span>
      </div>
      {task.comments?.length>0&&<div className="mt-1.5 text-[10px] text-gray-400">💬 {task.comments.length}</div>}
    </div>
  )
}

// ─── Task Form Modal ────────────────────────────────────────────────────────────
function TaskFormModal({ task, onClose, onSave, t, lang, users=[] }) {
  const isEdit = !!task?.id
  const [form, setForm] = useState({
    title:task?.title||'', desc:task?.desc||'', dept:task?.dept||'',
    assignedTo:task?.assignedTo||'', priority:task?.priority||'medium',
    due:task?.due||'', tags:(task?.tags||[]).join('، '),
  })
  const [errors, setErrors] = useState({})
  const set = (k,v) => { setForm(p=>({...p,[k]:v})); setErrors(p=>({...p,[k]:''})) }

  const validate = () => {
    const e = {}
    if (!form.title.trim()) e.title = t('required')
    if (!form.dept)         e.dept  = t('required')
    if (!form.due)          e.due   = t('required')
    setErrors(e); return Object.keys(e).length===0
  }

  const handleSave = () => {
    if (!validate()) return
    const u = users.find(u=>u.id===Number(form.assignedTo))
    onSave({ ...task, ...form, assignedTo:Number(form.assignedTo)||null, assignedName:u?.name||'',
      tags:form.tags.split(/[،,]/).map(t=>t.trim()).filter(Boolean),
      status:task?.status||'new', created:task?.created||new Date().toISOString().split('T')[0],
      createdBy:task?.createdBy||'أنت', comments:task?.comments||[], attachments:task?.attachments||[],
      escalated:task?.escalated||false, escalations:task?.escalations||[],
      history:task?.history||[{status:'new',date:new Date().toISOString().split('T')[0]}],
    })
    onClose()
  }

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4 overflow-y-auto" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-xl my-4" onClick={e=>e.stopPropagation()}>
        <div className="p-5 border-b border-gray-100 flex items-center justify-between">
          <h2 className="font-black text-gray-900">{isEdit?t('edit_task'):t('new_task')}</h2>
          <button onClick={onClose} className="w-8 h-8 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
        </div>
        <div className="p-5 space-y-4">
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1">{t('task_title_field')} <span className="text-red-400">*</span></label>
            <input value={form.title} onChange={e=>set('title',e.target.value)} className={INP(errors.title)} placeholder={t('title_placeholder')}/>
            {errors.title&&<p className="text-red-500 text-[10px] mt-0.5">{errors.title}</p>}
          </div>
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1">{t('task_desc')}</label>
            <textarea value={form.desc} onChange={e=>set('desc',e.target.value)} rows={3} className={`${INP()} resize-none`} placeholder={t('desc_placeholder')}/>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-1">{t('department')} <span className="text-red-400">*</span></label>
              <select value={form.dept} onChange={e=>set('dept',e.target.value)} className={INP(errors.dept)}>
                <option value="">{lang==='en'?'— Select Dept —':'— اختر القسم —'}</option>
                {DEPARTMENTS.map(d=><option key={d}>{d}</option>)}
              </select>
              {errors.dept&&<p className="text-red-500 text-[10px] mt-0.5">{errors.dept}</p>}
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-1">{t('assign_to')}</label>
              <select value={form.assignedTo} onChange={e=>set('assignedTo',e.target.value)} className={INP()}>
                <option value="">{lang==='en'?'— Select Employee —':'— اختر الموظف —'}</option>
                {users.map(u=><option key={u.id} value={u.id}>{u.name}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-1">{t('priority')}</label>
              <select value={form.priority} onChange={e=>set('priority',e.target.value)} className={INP()}>
                {PRIORITIES.map(p=><option key={p.key} value={p.key}>{lang==='en'?(p.labelEn||p.labelAr):p.labelAr}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-1">{t('due_date')} <span className="text-red-400">*</span></label>
              <input type="date" value={form.due} onChange={e=>set('due',e.target.value)} className={INP(errors.due)} min={new Date().toISOString().split('T')[0]}/>
              {errors.due&&<p className="text-red-500 text-[10px] mt-0.5">{errors.due}</p>}
            </div>
          </div>
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1">{t('tags')}</label>
            <input value={form.tags} onChange={e=>set('tags',e.target.value)} className={INP()} placeholder={t('tags_placeholder')}/>
          </div>
        </div>
        <div className="p-5 border-t border-gray-100 flex gap-3">
          <button onClick={onClose} className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50">{t('cancel')}</button>
          <button onClick={handleSave} className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 transition-colors">
            {isEdit?'💾 '+t('save_edits'):'✅ '+t('create_task')}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Task Detail Panel ─────────────────────────────────────────────────────────
function TaskDetail({ task, onClose, onUpdate, onDelete, isAdmin, t, lang }) {
  const [comment, setComment] = useState('')
  const [addingComment, setAC] = useState(false)
  const [showEscModal, setSEM]  = useState(false)
  const [showReturnModal, setSRM] = useState(false)
  const [returnNote, setRN]       = useState('')
  const [showAssignModal, setSAM] = useState(false)
  const fileRef = useRef()
  const s = STATUS_MAP[task.status]||STATUS_MAP.new
  const p = PRIO_MAP[task.priority]||PRIO_MAP.medium
  const sl = lang==='en'?(s.labelEn||s.labelAr):s.labelAr
  const pl = lang==='en'?(p.labelEn||p.labelAr):p.labelAr
  const isOverdue = task.due&&new Date(task.due)<new Date()&&!['completed','cancelled'].includes(task.status)

  const NEXT = { new:['assigned'], assigned:['inprogress','cancelled'], inprogress:['review','cancelled'], review:['completed','returned'], overdue:['inprogress','cancelled'], returned:['inprogress','cancelled'] }
  const BTN_CFG = {
    assigned:   {label:t('start_task'),   cls:'bg-blue-700 text-white hover:bg-blue-800'},
    inprogress: {label:t('send_review'),  cls:'bg-purple-600 text-white hover:bg-purple-700'},
    review:     {label:t('close_approve'),cls:'bg-green-600 text-white hover:bg-green-700'},
    returned:   {label:lang==='en'?'Restart Task':'إعادة التنفيذ', cls:'bg-blue-600 text-white hover:bg-blue-700'},
    cancelled:  {label:t('cancel_task'),  cls:'bg-gray-500 text-white hover:bg-gray-600'},
  }

  const changeStatus = (ns) => onUpdate({...task,status:ns,history:[...task.history,{status:ns,date:new Date().toISOString().split('T')[0]}]})
  const addComment   = () => {
    if (!comment.trim()) return
    onUpdate({...task,comments:[...task.comments,{id:Date.now(),by:'أنت',text:comment.trim(),date:new Date().toISOString().split('T')[0]}]})
    setComment(''); setAC(false)
  }
  const addFiles = (e) => onUpdate({...task,attachments:[...(task.attachments||[]),...Array.from(e.target.files||[]).map(f=>({name:f.name,size:(f.size/1024).toFixed(0)+'KB',date:new Date().toISOString().split('T')[0]}))]})

  return (
    <div className="fixed inset-0 z-40 md:relative md:inset-auto bg-white border-r border-gray-100 flex flex-col" style={{width:'100%',height:'100%'}}>
      {/* ── Return with Notes Modal ── */}
      {showReturnModal&&(
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[70] p-4" onClick={()=>setSRM(false)}>
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md" onClick={e=>e.stopPropagation()}>
            <div className="p-5 border-b border-gray-100 flex items-center gap-3">
              <div className="w-10 h-10 bg-pink-50 rounded-xl flex items-center justify-center text-2xl">↩️</div>
              <div>
                <h2 className="font-black text-gray-900">{lang==='en'?'Return Task for Revision':'إرجاع المهمة للتعديل'}</h2>
                <p className="text-xs text-gray-400 mt-0.5">{lang==='en'?'Task will be returned to assignee with your notes':'ستُرجع المهمة للمُكلَّف مع ملاحظاتك'}</p>
              </div>
              <button onClick={()=>setSRM(false)} className="mr-auto w-8 h-8 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
            </div>
            <div className="p-5 space-y-4">
              <div className="bg-pink-50 border border-pink-100 rounded-xl p-3">
                <p className="text-xs font-bold text-pink-800 mb-1">{lang==='en'?'Task:':'المهمة:'}</p>
                <p className="text-sm text-gray-800 font-semibold">{task.title}</p>
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-700 mb-1.5">
                  {lang==='en'?'Revision Notes':'ملاحظات التعديل'} <span className="text-red-400">*</span>
                </label>
                <textarea value={returnNote} onChange={e=>setRN(e.target.value)} rows={4}
                  placeholder={lang==='en'?'Explain what needs to be revised...':'وضّح ما يجب تعديله أو إضافته...'}
                  className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-pink-400 resize-none" dir={lang==='en'?'ltr':'rtl'}/>
              </div>
              <div className="bg-blue-50 border border-blue-100 rounded-xl p-3 text-xs text-blue-700">
                ℹ️ {lang==='en'?'The assignee will receive a notification with your notes':'سيتلقى المُكلَّف إشعاراً بملاحظاتك'}
              </div>
            </div>
            <div className="p-5 border-t border-gray-100 flex gap-3">
              <button onClick={()=>setSRM(false)} className="border border-gray-200 text-gray-600 px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50">{t('cancel')}</button>
              <button disabled={!returnNote.trim()} onClick={()=>{
                if (!returnNote.trim()) return
                // Add as comment + change status
                const comment = { id:Date.now(), by:'أنت', text:(lang==='en'?'🔄 Returned for revision: ':'🔄 إرجاع للتعديل: ')+returnNote.trim(), date:new Date().toISOString().split('T')[0], type:'return' }
                onUpdate({ ...task, status:'returned',
                  comments:[...task.comments, comment],
                  history:[...task.history, {status:'returned', date:new Date().toISOString().split('T')[0], note:returnNote.trim()}]
                })
                setSRM(false); setRN('')
              }}
                className="flex-1 bg-pink-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-pink-700 disabled:opacity-40 transition-colors">
                ↩️ {lang==='en'?'Return Task':'إرجاع المهمة'}
              </button>
            </div>
          </div>
        </div>
      )}
      {showAssignModal&&(
        <AssignModal task={task} onClose={()=>setSAM(false)} t={t} lang={lang}
          onAssign={updated=>{onUpdate(updated);setSAM(false)}}/>
      )}
      {showEscModal&&<EscalationModal task={task} userRole={isAdmin?3:1} onClose={()=>setSEM(false)} t={t} lang={lang}
        onEscalate={esc=>onUpdate({...task,escalated:true,escalations:[...(task.escalations||[]),esc],history:[...task.history,{status:'escalated',date:new Date().toISOString().split('T')[0]}]})}/>}
      <div className="p-4 border-b border-gray-100 flex-shrink-0" style={{background:s.bg}}>
        <div className="flex items-start justify-between mb-2">
          <div className="flex gap-1.5 flex-wrap">
            <span className="text-xs font-bold px-2.5 py-1 rounded-full" style={{background:'white',color:s.color}}>{s.icon} {sl}</span>
            <span className="text-xs font-bold px-2.5 py-1 rounded-full" style={{background:p.bg,color:p.color}}>{pl}</span>
            {task.escalated&&<span className="text-xs font-bold px-2.5 py-1 rounded-full bg-red-100 text-red-700">🔺</span>}
          {task.assignRole&&(()=>{const r=ASSIGNMENT_ROLES.find(ar=>ar.key===task.assignRole); return r?(
            <span className="text-xs font-bold px-2.5 py-1 rounded-full" style={{background:r.bg,color:r.color}}>
              {r.icon} {lang==='en'?(r.labelEn||r.labelAr):r.labelAr}
            </span>
          ):null})()}
          </div>
          <button onClick={onClose} className="w-9 h-9 rounded-xl bg-gray-100 hover:bg-gray-200 flex items-center justify-center text-gray-600 text-lg">✕</button>
        </div>
        <h2 className="font-black text-gray-900 leading-snug">{task.title}</h2>
        <div className="flex items-center gap-3 mt-1.5 text-xs text-gray-500 flex-wrap">
          {task.assignedName&&<span>👤 {task.assignedName}</span>}
          <span>🏛 {task.dept}</span>
          <span className={isOverdue?'text-red-600 font-semibold':''}>{isOverdue?'⚠️ ':''}{task.due}</span>
        </div>
      </div>
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {task.desc&&<div><p className="text-xs font-black text-gray-400 uppercase mb-1.5">{t('description')}</p><p className="text-sm text-gray-700 leading-relaxed bg-gray-50 rounded-xl p-3">{task.desc}</p></div>}
        {task.tags?.length>0&&<div className="flex flex-wrap gap-1.5">{task.tags.map(tg=><span key={tg} className="bg-blue-50 text-blue-600 text-[10px] px-2 py-0.5 rounded-full border border-blue-100">#{tg}</span>)}</div>}
        {/* Actions */}
        {(NEXT[task.status]||[]).length>0&&(
          <div>
            <p className="text-xs font-black text-gray-400 uppercase mb-2">{t('status_actions')}</p>
            <div className="flex flex-wrap gap-2">
              {(NEXT[task.status]||[]).map(ns=>{
                const b=BTN_CFG[ns]; if(!b) return null
                // Return action needs a note modal
                if(ns==='returned') return (
                  <button key={ns} onClick={()=>setSRM(true)}
                    className="text-xs font-bold px-3 py-2 rounded-xl transition-colors border-2 border-pink-300 text-pink-700 hover:bg-pink-50">
                    ↩️ {lang==='en'?'Return with Notes':'إرجاع مع ملاحظات'}
                  </button>
                )
                return <button key={ns} onClick={()=>changeStatus(ns)} className={`text-xs font-bold px-3 py-2 rounded-xl transition-colors ${b.cls}`}>{b.label}</button>
              })}
              {!task.escalated&&!['completed','cancelled'].includes(task.status)&&(
                <button onClick={()=>setSEM(true)} className="text-xs font-bold px-3 py-2 rounded-xl border-2 border-red-200 text-red-600 hover:bg-red-50">🔺 {t('escalate')}</button>
              )}
              {/* Assign button — always available */}
              <button onClick={()=>setSAM(true)}
                className="text-xs font-bold px-3 py-2 rounded-xl border-2 border-blue-200 text-blue-600 hover:bg-blue-50 transition-colors">
                👤 {lang==='en'?'Assign':'إسناد'}
              </button>
              {/* Delete button — always visible in detail (admin) or own tasks */}
              <button onClick={()=>onDelete&&onDelete(task.id)}
                className="text-xs font-bold px-3 py-2 rounded-xl border-2 border-gray-200 text-gray-500 hover:bg-red-50 hover:border-red-200 hover:text-red-600 transition-colors">
                🗑️ {t('delete')}
              </button>
            </div>
          </div>
        )}
        {/* History */}
        <div>
          <p className="text-xs font-black text-gray-400 uppercase mb-2">{t('status_history')}</p>
          <div className="space-y-1.5 border-r-2 border-gray-100 pr-3">
            {[...task.history].reverse().map((h,i)=>{const hs=STATUS_MAP[h.status]; const hl=lang==='en'?(hs?.labelEn||h.status):hs?.labelAr||h.status; return(
              <div key={i} className="flex items-center gap-2 text-xs">
                <div className="w-2 h-2 rounded-full flex-shrink-0 -mr-4" style={{background:hs?.dot||'#9ca3af'}}/>
                <span className="font-medium" style={{color:hs?.color||'#6b7280'}}>{hl}</span>
                <span className="text-gray-400">{h.date}</span>
              </div>
            )})}
          </div>
        </div>
        {/* Escalation history */}
        {task.escalations?.length>0&&(
          <div>
            <p className="text-xs font-black text-gray-400 uppercase mb-2">{t('escalation_history')}</p>
            <div className="space-y-2">
              {task.escalations.map((e,i)=>(
                <div key={i} className="bg-red-50 border border-red-100 rounded-xl p-3 text-xs">
                  <div className="flex items-center justify-between mb-1">
                    <span className="font-bold text-red-700">🔺 {t('esc_level')} {e.level}</span>
                    <span className={`px-2 py-0.5 rounded-full font-medium ${e.status==='pending'?'bg-amber-100 text-amber-700':'bg-green-100 text-green-700'}`}>{e.status==='pending'?t('esc_pending'):t('esc_resolved')}</span>
                  </div>
                  <p className="text-gray-600">{e.fromUser} ({e.fromRole}) → {e.toUser} ({e.toRole})</p>
                  <p className="text-gray-500 mt-0.5">{t('esc_reason_label')} {e.reason}</p>
                  <p className="text-gray-400 mt-0.5">{e.date}</p>
                </div>
              ))}
            </div>
          </div>
        )}
        {/* Attachments */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <p className="text-xs font-black text-gray-400 uppercase">{t('attachments')} ({task.attachments?.length||0})</p>
            <button onClick={()=>fileRef.current?.click()} className="text-xs text-blue-600 hover:text-blue-800 font-semibold">+ {t('add_attachment')}</button>
          </div>
          <input ref={fileRef} type="file" multiple className="hidden" onChange={addFiles}/>
          {task.attachments?.length>0?<div className="space-y-1.5">{task.attachments.map((a,i)=><div key={i} className="flex items-center gap-2 p-2 bg-gray-50 rounded-xl text-xs"><span>📎</span><span className="flex-1 font-medium truncate">{a.name}</span><span className="text-gray-400">{a.size}</span></div>)}</div>
          :<p className="text-xs text-gray-400 text-center py-2">{t('no_attachments')}</p>}
        </div>
        {/* Comments */}
        <div>
          <div className="flex items-center justify-between mb-2">
            <p className="text-xs font-black text-gray-400 uppercase">{t('comments')} ({task.comments?.length||0})</p>
            <button onClick={()=>setAC(p=>!p)} className="text-xs text-blue-600 hover:text-blue-800 font-semibold">+ {t('add_comment')}</button>
          </div>
          {addingComment&&<div className="mb-3 space-y-2">
            <textarea value={comment} onChange={e=>setComment(e.target.value)} rows={2} placeholder={t('comment_placeholder')} className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 resize-none" dir={lang==='en'?'ltr':'rtl'}/>
            <div className="flex gap-2">
              <button onClick={addComment} className="flex-1 bg-blue-700 text-white text-xs py-2 rounded-xl font-bold hover:bg-blue-800">{t('add')}</button>
              <button onClick={()=>{setAC(false);setComment('')}} className="border border-gray-200 text-gray-500 text-xs px-3 py-2 rounded-xl hover:bg-gray-50">{t('cancel')}</button>
            </div>
          </div>}
          {task.comments?.length>0?<div className="space-y-2">{task.comments.map(c=><div key={c.id} className="bg-gray-50 rounded-xl p-3"><div className="flex items-center justify-between text-[10px] text-gray-400 mb-1"><span className="font-bold text-gray-600">👤 {c.by}</span><span>{c.date}</span></div><p className="text-xs text-gray-700">{c.text}</p></div>)}</div>
          :!addingComment&&<p className="text-xs text-gray-400 text-center py-2">{t('no_comments')}</p>}
        </div>
        <div className="text-xs text-gray-400 space-y-1 pt-2 border-t border-gray-100">
          <div className="flex justify-between"><span>{t('task_created_at')}</span><span>{task.created}</span></div>
          <div className="flex justify-between"><span>{t('task_created_by')}</span><span>{task.createdBy}</span></div>
          {task.assignedName&&<div className="flex justify-between"><span>{t('task_assigned_to')}</span><span>{task.assignedName}</span></div>}
        </div>
      </div>
    </div>
  )
}

// ─── Main Page ──────────────────────────────────────────────────────────────────
export default function TasksPage() {
  const { show, ToastContainer }    = useToast()
  const { user }                    = useAuthStore()
  const { lang, t, isRTL }         = useLang()
  const [tasks, setTasks]           = useLocalStorage('ecm_tasks_v2', MOCK_TASKS)
  const [selected, setSelected]     = useState(null)
  const [showCreate, setShowCreate] = useState(false)
  const [editTask, setEditTask]     = useState(null)
  const [filterStatus, setFS]       = useState('all')
  const [filterPrio, setFP]         = useState('all')
  const [filterDept, setFD]         = useState('all')
  const [search, setSearch]         = useState('')
  const [view, setView]             = useState('board')
  const [showReports, setSR]        = useState(false)
  const [showFilters, setSF]        = useState(false)
  const [draggingId, setDraggingId]   = useState(null)
  const [dragOverCol, setDragOverCol] = useState(null)

  const { activeUsers: USERS } = useUsers()
  const isAdmin = (user?.permissions||[]).some(p=>p==='admin.*')
  const safeTasks = Array.isArray(tasks) ? tasks : MOCK_TASKS
  const sl = (s) => lang==='en'?(s.labelEn||s.labelAr):s.labelAr

  // Auto-clean tasks: remove assignee if user no longer exists
  const cleanedTasks = React.useMemo(() => {
    return safeTasks.map(tk => {
      if (!tk.assignedTo) return tk
      const exists = USERS.find(u => u.id === tk.assignedTo)
      if (exists) return tk
      // User was deleted — clear assignee, add history entry
      return {
        ...tk,
        assignedTo: null,
        assignedName: '',
        assignRole: null,
        history: [...(tk.history||[]), {
          status: tk.status,
          date: new Date().toISOString().split('T')[0],
          note: lang==='en'?'Assignee removed (user deleted)':'تم إزالة المُكلَّف (المستخدم حُذف)',
        }]
      }
    })
  }, [safeTasks, USERS])

  // Which columns a task can be dragged TO (forward + backward)
  const ALLOWED_TRANSITIONS = {
    new:        ['assigned'],
    assigned:   ['new','inprogress','cancelled'],
    inprogress: ['assigned','review','cancelled'],
    review:     ['inprogress','completed','returned'],
    returned:   ['inprogress','cancelled'],
    completed:  [],          // completed tasks cannot be moved
    cancelled:  [],
    overdue:    ['inprogress','cancelled'],
  }

  const canDropTo = (taskStatus, targetCol) => {
    if (!taskStatus || !targetCol) return false
    return (ALLOWED_TRANSITIONS[taskStatus] || []).includes(targetCol)
  }

  const handleDrop = (targetColKey, e) => {
    e.preventDefault()
    setDragOverCol(null)
    if (!draggingId) return
    const task = withOverdue.find(t => t.id === draggingId)
    if (!task) return
    if (!canDropTo(task.status, targetColKey)) return
    const history = [...task.history, {
      status: targetColKey,
      date: new Date().toISOString().split('T')[0],
      movedBy: 'أنت',
      via: 'drag'
    }]
    updateTask({ ...task, status: targetColKey, history })
    show(
      lang === 'en'
        ? `Task moved to: ${STATUSES.find(s=>s.key===targetColKey)?.labelEn}`
        : `تم نقل المهمة إلى: ${STATUSES.find(s=>s.key===targetColKey)?.labelAr}`,
      'success'
    )
    setDraggingId(null)
  }
  const pl = (p) => lang==='en'?(p.labelEn||p.labelAr):p.labelAr

  const withOverdue = cleanedTasks.map(t=>({...t,status:!['completed','cancelled'].includes(t.status)&&t.due&&new Date(t.due)<new Date()?'overdue':t.status}))
  const filtered = withOverdue.filter(t=>(filterStatus==='all'||t.status===filterStatus)&&(filterPrio==='all'||t.priority===filterPrio)&&(filterDept==='all'||t.dept===filterDept)&&(!search||t.title.includes(search)||(t.assignedName||'').includes(search)||(t.dept||'').includes(search)))

  const updateTask = (u) => { setTasks(p=>(Array.isArray(p)?p:MOCK_TASKS).map(t=>t.id===u.id?u:t)); if(selected?.id===u.id) setSelected(u) }
  const saveTask = (task) => {
    if (task.id) { updateTask(task); show('✅ '+t('save_edits'), 'success') }
    else { const n={...task,id:Date.now()}; setTasks(p=>[n,...(Array.isArray(p)?p:MOCK_TASKS)]); show('✅ '+t('new_task'), 'success'); client.post('/api/v1/tasks',n).catch(()=>{}) }
  }
  const deleteTask = (id) => {
    const tk = safeTasks.find(t=>t.id===id)
    if (tk?.status==='completed') { show(lang==='en'?'Cannot delete completed task':'لا يمكن حذف مهمة مكتملة','error'); return }
    setTasks(p=>(Array.isArray(p)?p:[]).filter(t=>t.id!==id)); setSelected(null); show(t('delete'), 'success')
  }

  const boardCols = STATUSES.filter(s=>!['overdue','cancelled'].includes(s.key)).map(s=>({...s,label:sl(s),tasks:filtered.filter(t=>t.status===s.key)}))
  const overdueTasks = filtered.filter(t=>t.status==='overdue')
  const cancelledTasks = filtered.filter(t=>t.status==='cancelled')
  const stats = { total:safeTasks.length, inprogress:withOverdue.filter(t=>t.status==='inprogress').length, overdue:withOverdue.filter(t=>t.status==='overdue').length, completed:withOverdue.filter(t=>t.status==='completed').length, escalated:withOverdue.filter(t=>t.escalated).length }

  return (
    <div className="flex flex-col h-full">
      <ToastContainer/>
      {(showCreate||editTask)&&<TaskFormModal task={editTask} onClose={()=>{setShowCreate(false);setEditTask(null)}} onSave={saveTask} t={t} lang={lang} users={USERS}/>}

      <div className="flex-shrink-0 space-y-3 mb-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-black text-gray-900">{t('tasks_title')}</h1>
            <p className="text-sm text-gray-400 mt-0.5">{stats.total} {lang==='en'?'tasks':'مهمة'} · {stats.overdue} {t('st_overdue')} · {stats.escalated} {t('task_kpi_escalated')}</p>
          </div>
          <div className="flex gap-2">
            <button onClick={()=>setSR(p=>!p)} className={`border text-sm px-4 py-2 rounded-xl transition-colors ${showReports?'bg-gray-900 text-white border-gray-900':'border-gray-200 text-gray-600 hover:bg-gray-50'}`}>📊 {t('reports')}</button>
            <button onClick={()=>{setShowCreate(true);setEditTask(null)}} className="bg-blue-700 text-white px-5 py-2 rounded-xl text-sm font-bold hover:bg-blue-800 shadow-sm">+ {t('new_task')}</button>
          </div>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2">
          {[[t('task_kpi_total'),stats.total,'📋','bg-indigo-50 text-indigo-700 border-indigo-100'],
            [t('task_kpi_inprog'),stats.inprogress,'🔄','bg-amber-50 text-amber-700 border-amber-100'],
            [t('task_kpi_overdue'),stats.overdue,'⚠️','bg-red-50 text-red-700 border-red-100'],
            [t('task_kpi_done'),stats.completed,'✅','bg-green-50 text-green-700 border-green-100'],
            [t('task_kpi_escalated'),stats.escalated,'🔺','bg-rose-50 text-rose-700 border-rose-100'],
          ].map(([l,v,ic,cls])=>(
            <div key={l} className={`${cls} border rounded-2xl p-3 flex items-center gap-2.5`}>
              <span className="text-2xl">{ic}</span><div><p className="text-xl font-black">{v}</p><p className="text-[10px] opacity-80">{l}</p></div>
            </div>
          ))}
        </div>

        {showReports&&(
          <div className="bg-white rounded-2xl border border-gray-100 p-5 shadow-sm">
            <p className="font-bold text-gray-800 mb-4">📊 {t('tasks_reports_title')}</p>
            <div className="grid grid-cols-2 gap-6">
              <div>
                <p className="text-xs font-black text-gray-500 mb-2 uppercase">{t('by_status')}</p>
                {STATUSES.map(s=>{const cnt=withOverdue.filter(t=>t.status===s.key).length; return(
                  <div key={s.key} className="flex items-center gap-2 mb-2">
                    <div className="w-2 h-2 rounded-full flex-shrink-0" style={{background:s.dot}}/>
                    <span className="text-xs text-gray-600 flex-1">{sl(s)}</span>
                    <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden"><div className="h-full rounded-full" style={{width:`${stats.total?cnt/stats.total*100:0}%`,background:s.color}}/></div>
                    <span className="text-xs font-bold text-gray-700 w-4">{cnt}</span>
                  </div>
                )})}
              </div>
              <div>
                <p className="text-xs font-black text-gray-500 mb-2 uppercase">{t('by_dept')}</p>
                {DEPARTMENTS.filter(d=>withOverdue.some(t=>t.dept===d)).map(d=>{const cnt=withOverdue.filter(t=>t.dept===d).length; return(
                  <div key={d} className="flex items-center gap-2 mb-2">
                    <span className="text-xs text-gray-600 flex-1 truncate">{d}</span>
                    <div className="w-20 h-2 bg-gray-100 rounded-full overflow-hidden"><div className="h-full bg-blue-400 rounded-full" style={{width:`${stats.total?cnt/stats.total*100:0}%`}}/></div>
                    <span className="text-xs font-bold text-gray-700 w-4">{cnt}</span>
                  </div>
                )})}
              </div>
            </div>
          </div>
        )}

        {/* Filters */}
        <div>
          <button onClick={()=>setSF(p=>!p)} className="md:hidden w-full flex items-center justify-between bg-white border border-gray-200 rounded-xl px-4 py-2.5 text-sm font-medium text-gray-700 mb-2">
            <span>🔍 {t('filter')}</span><span>{showFilters?'▲':'▼'}</span>
          </button>
          <div className={`bg-white rounded-2xl border border-gray-100 p-3 flex gap-2 flex-wrap ${showFilters?'flex':'hidden md:flex'}`}>
            <div className="relative flex-1 min-w-36">
              <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
              <input value={search} onChange={e=>setSearch(e.target.value)} placeholder={t('search_tasks')} className="w-full pr-8 pl-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"/>
            </div>
            <select value={filterStatus} onChange={e=>setFS(e.target.value)} className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
              <option value="all">{t('filter_all_status')}</option>
              {STATUSES.map(s=><option key={s.key} value={s.key}>{sl(s)}</option>)}
            </select>
            <select value={filterPrio} onChange={e=>setFP(e.target.value)} className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
              <option value="all">{t('filter_all_prio')}</option>
              {PRIORITIES.map(p=><option key={p.key} value={p.key}>{pl(p)}</option>)}
            </select>
            <select value={filterDept} onChange={e=>setFD(e.target.value)} className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none">
              <option value="all">{t('filter_all_dept')}</option>
              {DEPARTMENTS.map(d=><option key={d}>{d}</option>)}
            </select>
            <div className="flex border border-gray-200 rounded-xl overflow-hidden">
              <button onClick={()=>setView('board')} className={`px-3 py-2 text-sm ${view==='board'?'bg-gray-900 text-white':'text-gray-500 hover:bg-gray-50'}`}>{t('board_view')}</button>
              <button onClick={()=>setView('list')}  className={`px-3 py-2 text-sm ${view==='list'?'bg-gray-900 text-white':'text-gray-500 hover:bg-gray-50'}`}>{t('list_view')}</button>
            </div>
          </div>
        </div>
      </div>

      <div className="flex-1 flex gap-4 overflow-hidden min-h-0">
        {view==='board'&&(
          <div className="flex-1 overflow-x-auto overflow-y-hidden">
            <div className="flex gap-4 h-full pb-2" style={{minWidth:(boardCols.length+1)*240}}>
              {boardCols.map(col=>(
                <div key={col.key} className="flex-shrink-0 flex flex-col rounded-2xl overflow-hidden" style={{width:240,background:col.bg}}>
                  <div className="px-3 py-2.5 flex items-center gap-2 flex-shrink-0 border-b" style={{borderColor:col.color+'30'}}>
                    <span>{col.icon}</span><span className="text-xs font-black flex-1" style={{color:col.color}}>{col.label}</span>
                    <span className="text-xs font-bold px-1.5 py-0.5 rounded-full" style={{background:col.color,color:'white'}}>{col.tasks.length}</span>
                  </div>
                  <div
                  className={`flex-1 overflow-y-auto p-2 space-y-2 transition-all rounded-b-2xl ${
                    dragOverCol===col.key && draggingId
                      ? 'bg-white/60 ring-2 ring-inset ring-blue-400'
                      : ''
                  }`}
                  onDragOver={e=>{ e.preventDefault(); const task=withOverdue.find(t=>t.id===draggingId); if(task&&canDropTo(task.status,col.key)){e.dataTransfer.dropEffect='move';setDragOverCol(col.key)}else{e.dataTransfer.dropEffect='none'} }}
                  onDragLeave={()=>setDragOverCol(null)}
                  onDrop={e=>handleDrop(col.key,e)}>
                  {col.tasks.map(tk=><TaskCard key={tk.id} task={tk} lang={lang}
                    selected={selected?.id===tk.id}
                    onClick={()=>setSelected(selected?.id===tk.id?null:tk)}
                    onDragStart={id=>setDraggingId(id)}
                    onDragEnd={()=>{setDraggingId(null);setDragOverCol(null)}}/>)}
                  {col.tasks.length===0&&(
                    <div className={`text-center py-6 text-gray-400 text-xs rounded-xl border-2 border-dashed transition-all ${
                      dragOverCol===col.key&&draggingId&&canDropTo(withOverdue.find(t=>t.id===draggingId)?.status,col.key)
                        ?'border-blue-400 bg-blue-50/50 text-blue-400'
                        :'border-transparent'
                    }`}>
                      {dragOverCol===col.key&&draggingId?'↓ أفلت هنا':''}
                      {dragOverCol!==col.key&&t('no_tasks')}
                    </div>
                  )}
                </div>
                </div>
              ))}
              {(overdueTasks.length>0||cancelledTasks.length>0)&&(
                <div className="flex-shrink-0 flex flex-col gap-2" style={{width:240}}>
                  {overdueTasks.length>0&&(
                    <div className="flex-shrink-0 rounded-2xl overflow-hidden" style={{background:'#fef2f2'}}>
                      <div className="px-3 py-2 flex items-center gap-2 border-b border-red-100"><span>⚠️</span><span className="text-xs font-black text-red-600 flex-1">{t('st_overdue')}</span><span className="text-xs font-bold px-1.5 py-0.5 rounded-full bg-red-600 text-white">{overdueTasks.length}</span></div>
                      <div className="p-2 space-y-2 max-h-64 overflow-y-auto">{overdueTasks.map(tk=><TaskCard key={tk.id} task={tk} lang={lang} selected={selected?.id===tk.id} onClick={()=>setSelected(selected?.id===tk.id?null:tk)} onDragStart={id=>setDraggingId(id)} onDragEnd={()=>{setDraggingId(null);setDragOverCol(null)}}/>)}</div>
                    </div>
                  )}
                  {cancelledTasks.length>0&&(
                    <div className="flex-shrink-0 rounded-2xl overflow-hidden" style={{background:'#f9fafb'}}>
                      <div className="px-3 py-2 flex items-center gap-2 border-b border-gray-200"><span>⛔</span><span className="text-xs font-black text-gray-500 flex-1">{t('st_cancelled')}</span><span className="text-xs font-bold px-1.5 py-0.5 rounded-full bg-gray-500 text-white">{cancelledTasks.length}</span></div>
                      <div className="p-2 space-y-2 max-h-48 overflow-y-auto">{cancelledTasks.map(tk=><TaskCard key={tk.id} task={tk} lang={lang} selected={selected?.id===tk.id} onClick={()=>setSelected(selected?.id===tk.id?null:tk)}/>)}</div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        {view==='list'&&(
          <div className="flex-1 overflow-y-auto">
            {filtered.length===0?<div className="bg-white rounded-2xl border border-gray-100 p-16 text-center text-gray-400"><div className="text-5xl mb-3">📋</div><p>{t('no_tasks')}</p></div>:(
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
                <table className="w-full text-sm">
                  <thead><tr className="bg-gray-50 border-b border-gray-100">
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400">{lang==='en'?'Task':'المهمة'}</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">{t('department')}</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400">{t('status')}</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">{t('priority')}</th>
                    <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden lg:table-cell">{t('due_date')}</th>
                    {isAdmin&&<th className="px-4 py-3 text-right text-xs font-black text-gray-400">{lang==='en'?'Actions':'إجراء'}</th>}
                  </tr></thead>
                  <tbody className="divide-y divide-gray-50">
                    {filtered.map(tk=>{
                      const s=STATUS_MAP[tk.status]||STATUS_MAP.new; const p=PRIO_MAP[tk.priority]||PRIO_MAP.medium
                      const over=tk.due&&new Date(tk.due)<new Date()&&!['completed','cancelled'].includes(tk.status)
                      return(
                        <tr key={tk.id} onClick={()=>setSelected(selected?.id===tk.id?null:tk)} className={`cursor-pointer transition-colors ${selected?.id===tk.id?'bg-blue-50':'hover:bg-gray-50'}`}>
                          <td className="px-4 py-3"><p className="font-bold text-gray-900 truncate max-w-[200px]">{tk.title}</p><p className="text-[10px] text-gray-400">{tk.assignedName||'—'} {tk.escalated&&'· 🔺'}</p></td>
                          <td className="px-4 py-3 hidden md:table-cell text-xs text-gray-500">{tk.dept}</td>
                          <td className="px-4 py-3"><span className="text-[11px] px-2 py-0.5 rounded-full font-medium" style={{background:s.bg,color:s.color}}>{s.icon} {sl(s)}</span></td>
                          <td className="px-4 py-3 hidden md:table-cell"><span className="text-[11px] px-2 py-0.5 rounded-full font-medium" style={{background:p.bg,color:p.color}}>{pl(p)}</span></td>
                          <td className={`px-4 py-3 hidden lg:table-cell text-xs font-medium ${over?'text-red-600':''}`}>{over?'⚠️ ':''}{tk.due}</td>
                          {isAdmin&&<td className="px-4 py-3"><div className="flex gap-2"><button onClick={e=>{e.stopPropagation();setEditTask(tk)}} className="text-xs text-blue-600 hover:underline">{t('edit')}</button><button onClick={e=>{e.stopPropagation();deleteTask(tk.id)}} className="text-xs text-red-500 hover:underline">{t('delete')}</button></div></td>}
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {selected&&!showCreate&&!editTask&&(
          <div className={`${view==='board'?'hidden lg:block':'hidden md:block'} flex-shrink-0`} style={{width:420,height:'100%'}}>
            <TaskDetail task={selected} isAdmin={isAdmin} onClose={()=>setSelected(null)} onUpdate={updateTask} onDelete={deleteTask} t={t} lang={lang}/>
          </div>
        )}
        {selected&&!showCreate&&!editTask&&(
          <div className="md:hidden">
            <TaskDetail task={selected} isAdmin={isAdmin} onClose={()=>setSelected(null)} onUpdate={updateTask} onDelete={deleteTask} t={t} lang={lang}/>
          </div>
        )}
      </div>
    </div>
  )
}
