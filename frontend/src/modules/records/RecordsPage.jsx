import React, { useState, useEffect, useRef } from 'react'
import { useLang } from '../../i18n.js'
import client from '../../api/client'
import { useLocalStorage } from '../../hooks/useLocalStorage'
import { useToast } from '../../components/Toast'

// ─── Static schema (mirrors backend built-in) ─────────────────────────────────
const DOMAINS = [
  { id:1, code:'LEG', nameAr:'قانوني وتعاقدي', nameEn:'Legal & Contractual',  icon:'⚖️',  color:'#7c3aed', light:'#f5f3ff', border:'#c4b5fd' },
  { id:2, code:'FIN', nameAr:'مالي', nameEn:'Financial',             icon:'💰',  color:'#059669', light:'#ecfdf5', border:'#6ee7b7' },
  { id:3, code:'ADM', nameAr:'إداري', nameEn:'Administrative',            icon:'🏛️',  color:'#0369a1', light:'#eff6ff', border:'#93c5fd' },
  { id:4, code:'HIS', nameAr:'تاريخي وأرشيفي', nameEn:'Historical & Archival',  icon:'📜',  color:'#b45309', light:'#fffbeb', border:'#fcd34d' },
  { id:5, code:'RES', nameAr:'بحثي وأكاديمي', nameEn:'Research & Academic',   icon:'🔬',  color:'#0891b2', light:'#ecfeff', border:'#67e8f9' },
]

const TYPES_BY_DOMAIN = {
  1:[{id:101,nameAr:'عقد',icon:'📋'},{id:102,nameAr:'اتفاقية',icon:'🤝'},{id:103,nameAr:'ترخيص',icon:'📜'},{id:104,nameAr:'رأي قانوني',icon:'⚖️'}],
  2:[{id:201,nameAr:'ميزانية',icon:'📊'},{id:202,nameAr:'فاتورة',icon:'🧾'},{id:203,nameAr:'تقرير مالي',icon:'📈'},{id:204,nameAr:'مشتريات',icon:'🛒'}],
  3:[{id:301,nameAr:'خطاب',icon:'✉️'},{id:302,nameAr:'مذكرة داخلية',icon:'📝'},{id:303,nameAr:'تقرير إداري',icon:'📄'},{id:304,nameAr:'محضر اجتماع',icon:'🗓'},{id:305,nameAr:'سياسة',icon:'📋'}],
  4:[{id:401,nameAr:'مخطوطة',icon:'📜'},{id:402,nameAr:'صورة تاريخية',icon:'🖼'},{id:403,nameAr:'خريطة',icon:'🗺'},{id:404,nameAr:'وثيقة أرشيفية',icon:'📦'}],
  5:[{id:501,nameAr:'ورقة بحثية',icon:'🔬'},{id:502,nameAr:'رسالة علمية',icon:'🎓'},{id:503,nameAr:'دراسة',icon:'📚'},{id:504,nameAr:'مقالة',icon:'📰'}],
}

const CORE_FIELDS = [
  {key:'title_ar',     label:'العنوان بالعربية',     type:'text',     required:true},
  {key:'title_en',     label:'العنوان بالإنجليزية',  type:'text'},
  {key:'description',  label:'الوصف / الملخص',      type:'textarea'},
  {key:'department',   label:'الإدارة / القسم',     type:'text',     required:true},
  {key:'document_date',label:'تاريخ الوثيقة',       type:'date'},
  {key:'security',     label:'مستوى السرية',        type:'select',   opts:['عام','داخلي','سري','مقيد'], required:true},
  {key:'tags',         label:'الكلمات المفتاحية',   type:'tags'},
]

const DOMAIN_FIELDS = {
  1:[
    {key:'contract_number',labelAr:'رقم العقد',       type:'text',   required:true},
    {key:'counterparty',   labelAr:'الطرف الثاني',    type:'text',   required:true},
    {key:'contract_start', labelAr:'تاريخ البداية',   type:'date',   required:true},
    {key:'contract_end',   labelAr:'تاريخ النهاية',   type:'date',   required:true},
    {key:'contract_value', labelAr:'قيمة العقد',       type:'number'},
    {key:'currency',       labelAr:'العملة',           type:'select', opts:['SAR','USD','EUR','GBP']},
  ],
  2:[
    {key:'fiscal_year',  labelAr:'السنة المالية',  type:'number', required:true},
    {key:'cost_center',  labelAr:'مركز التكلفة',   type:'text'},
    {key:'amount',       labelAr:'المبلغ',          type:'number'},
    {key:'currency_fin', labelAr:'العملة',          type:'select', opts:['SAR','USD','EUR']},
    {key:'budget_chapter',labelAr:'باب الميزانية', type:'text'},
  ],
  3:[
    {key:'letter_number',labelAr:'رقم الخطاب',      type:'text'},
    {key:'sender',       labelAr:'الجهة المرسِلة',  type:'text'},
    {key:'receiver',     labelAr:'الجهة المستقبِلة',type:'text'},
    {key:'reference',    labelAr:'المرجع',           type:'text'},
    {key:'priority_adm', labelAr:'الأولوية',        type:'select', opts:['عاجل','مهم','عادي']},
  ],
  4:[
    {key:'historical_period',labelAr:'الحقبة التاريخية',   type:'select', opts:['ما قبل الإسلام','صدر الإسلام','الدولة الأولى','الدولة الثانية','المملكة الحديثة','المعاصر']},
    {key:'geo_location',     labelAr:'الموقع الجغرافي',    type:'text'},
    {key:'source_hist',      labelAr:'المصدر',              type:'text'},
    {key:'digitization',     labelAr:'حالة الرقمنة',        type:'select', opts:['لم يُرقَّم','قيد الرقمنة','مُرقَّم','منشور رقمياً']},
    {key:'condition',        labelAr:'حالة المادة الأصلية',type:'select', opts:['ممتازة','جيدة','متوسطة','تحتاج ترميم']},
  ],
  5:[
    {key:'author',      labelAr:'المؤلف / الباحث',   type:'text',   required:true},
    {key:'institution', labelAr:'المؤسسة / الجامعة', type:'text'},
    {key:'pub_year',    labelAr:'سنة النشر',          type:'number'},
    {key:'isbn_issn',   labelAr:'ISBN / ISSN',        type:'text'},
    {key:'language_res',labelAr:'لغة الدراسة',       type:'select', opts:['العربية','الإنجليزية','الفرنسية','أخرى']},
  ],
}

const STATUS_CFG = {
  'مسودة':{labelEn:'Draft',cls:'bg-gray-100 text-gray-600',   dot:'bg-gray-400',   label:'Draft'},
  'قيد المراجعة':{labelEn:'Under Review',cls:'bg-amber-100 text-amber-700', dot:'bg-amber-500',  label:'Review'},
  'معتمد':{labelEn:'Approved',cls:'bg-green-100 text-green-700', dot:'bg-green-500',  label:'Approved'},
  'مؤرشف':{labelEn:'Archived',cls:'bg-blue-100 text-blue-700',   dot:'bg-blue-500',   label:'Archived'},
  'مرفوض':{labelEn:'Rejected',cls:'bg-red-100 text-red-700',     dot:'bg-red-500',    label:'Rejected'},
}

const SECURITY_CFG = {
  'عام':   {cls:'bg-green-50 text-green-700 border-green-200',  icon:'🌐'},
  'داخلي': {cls:'bg-blue-50 text-blue-700 border-blue-200',     icon:'🔵'},
  'سري':   {cls:'bg-orange-50 text-orange-700 border-orange-200',icon:'🔒'},
  'مقيد':  {cls:'bg-red-50 text-red-700 border-red-200',         icon:'🔴'},
}

const MOCK_RECORDS = [
  {id:1, recordNumber:'DARAH-LEG-2026-11234', domainId:1, typeId:101, titleAr:'عقد صيانة الأنظمة الإلكترونية 2026', status:'قيد المراجعة', security:'سري',   dept:'تقنية المعلومات', createdAt:'2026-04-15', metadata:{contract_number:'2026-IT-001', counterparty:'شركة الحلول التقنية', contract_value:'450000', currency:'SAR'}},
  {id:2, recordNumber:'DARAH-FIN-2026-44521', domainId:2, typeId:203, titleAr:'التقرير المالي الربع الأول 2026',    status:'معتمد',        security:'داخلي',  dept:'الشؤون المالية',   createdAt:'2026-04-10', metadata:{fiscal_year:'2026', amount:'12500000', currency_fin:'SAR'}},
  {id:3, recordNumber:'DARAH-ADM-2026-77890', domainId:3, typeId:301, titleAr:'خطاب تهنئة اليوم الوطني 96',        status:'معتمد',        security:'عام',    dept:'الرئاسة التنفيذية',createdAt:'2026-04-08', metadata:{letter_number:'ADM-2026-096', sender:'المدير التنفيذي'}},
  {id:4, recordNumber:'DARAH-HIS-2026-33412', domainId:4, typeId:401, titleAr:'مخطوطة تاريخ نجد — القرن 12 هجري',  status:'مسودة',        security:'داخلي',  dept:'الأرشيف التاريخي', createdAt:'2026-04-05', metadata:{historical_period:'الدولة الأولى', condition:'تحتاج ترميم', digitization:'قيد الرقمنة'}},
  {id:5, recordNumber:'DARAH-RES-2026-55671', domainId:5, typeId:501, titleAr:'دراسة التحولات الاجتماعية في الجزيرة العربية', status:'مسودة', security:'عام', dept:'مركز الأبحاث', createdAt:'2026-04-01', metadata:{author:'د. خالد العمري', institution:'جامعة الملك سعود', pub_year:'2026'}},
]

// ─── Field Input component ────────────────────────────────────────────────────
function FieldInput({field, value, onChange}) {
  const base = 'w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 bg-white'
  const label = field.label || field.labelAr

  if (field.type === 'select') return (
    <select value={value||''} onChange={e=>onChange(e.target.value)} className={base}>
      <option value="">— اختر —</option>
      {(field.opts||[]).map(o=><option key={o}>{o}</option>)}
    </select>
  )
  if (field.type === 'textarea') return (
    <textarea value={value||''} onChange={e=>onChange(e.target.value)}
      rows={3} className={`${base} resize-none text-right`}/>
  )
  if (field.type === 'tags') return (
    <input value={value||''} onChange={e=>onChange(e.target.value)}
      placeholder="كلمة1، كلمة2، كلمة3 ..." className={`${base} text-right`}/>
  )
  if (field.type === 'number' || field.type === 'currency') return (
    <input type="number" value={value||''} onChange={e=>onChange(e.target.value)}
      className={`${base}`} dir="ltr"/>
  )
  if (field.type === 'date') return (
    <input type="date" value={value||''} onChange={e=>onChange(e.target.value)}
      className={base} dir="ltr" min="2000-01-01"/>
  )
  return (
    <input type="text" value={value||''} onChange={e=>onChange(e.target.value)}
      className={`${base} text-right`}/>
  )
}

// ─── Create Record Modal (5-step) ─────────────────────────────────────────────
function CreateRecordModal({onClose, onSuccess}) {
  const [step, setStep]         = useState(0)  // 0:domain 1:type 2:core 3:domain-fields 4:files
  const [domain, setDomain]     = useState(null)
  const [rtype, setRtype]       = useState(null)
  const [values, setValues]     = useState({security:'داخلي'})
  const [files, setFiles]       = useState([])
  const [loading, setLoading]   = useState(false)
  const fileRef = useRef()

  const set = (k,v) => setValues(p=>({...p,[k]:v}))

  const domainFields = domain ? (DOMAIN_FIELDS[domain.id]||[]) : []
  const STEPS = [lang==='en'?['Domain','Type','Core Data','Domain Fields','Files']:['النطاق','النوع','البيانات الأساسية','بيانات النطاق','المرفقات']]

  const canNext = [
    !!domain,
    !!rtype,
    !!(values.title_ar && values.department && values.security),
    true,
    true,
  ]

  const handleSubmit = async () => {
    setLoading(true)
    const meta = {...values}
    const payload = {
      titleAr: values.title_ar, titleEn: values.title_en,
      domainId: domain.id, typeId: rtype.id,
      department: values.department, metadataJson: JSON.stringify(meta)
    }
    try { await client.post('/api/v1/records', payload) } catch {}
    const newRec = {
      id: Date.now(), recordNumber: `DARAH-${domain.code}-${new Date().getFullYear()}-${Math.floor(Math.random()*90000+10000)}`,
      domainId: domain.id, typeId: rtype.id,
      titleAr: values.title_ar, titleEn: values.title_en,
      status: 'مسودة', security: values.security,
      dept: values.department,
      createdAt: new Date().toISOString().split('T')[0],
      metadata: meta,
      attachments: files.map(f=>({name:f.name, size:f.size})),
    }
    setLoading(false)
    onSuccess(newRec)
    onClose()
  }

  return (
    <div className="fixed inset-0 bg-black/60 flex items-start justify-center z-50 p-4 overflow-y-auto" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl my-4" onClick={e=>e.stopPropagation()}>

        {/* Header */}
        <div className="p-5 border-b border-gray-100">
          <div className="flex items-center justify-between mb-3">
            <div>
              <h2 className="font-black text-gray-900 text-lg">t('new_record')</h2>
              <p className="text-xs text-gray-400">منصة t('records_title') — دارة الملك عبدالعزيز</p>
            </div>
            <button onClick={onClose} className="w-9 h-9 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
          </div>
          {/* Progress */}
          <div className="flex items-center gap-1">
            {STEPS.map((s,i)=>(
              <React.Fragment key={i}>
                <div className={`flex items-center gap-1 text-xs font-medium ${i<step?'text-green-600':i===step?'text-blue-700':'text-gray-400'}`}>
                  <div className={`w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-black ${i<step?'bg-green-500 text-white':i===step?'bg-blue-600 text-white':'bg-gray-100 text-gray-400'}`}>
                    {i<step?'✓':i+1}
                  </div>
                  <span className="hidden sm:block">{s}</span>
                </div>
                {i<STEPS.length-1&&<div className={`flex-1 h-px mx-1 ${i<step?'bg-green-300':'bg-gray-100'}`}/>}
              </React.Fragment>
            ))}
          </div>
        </div>

        <div className="p-5 min-h-[300px]">

          {/* Step 0: Domain */}
          {step===0&&(
            <div>
              <p className="text-sm font-bold text-gray-700 mb-3">اختر النطاق <span className="text-red-400">*</span></p>
              <div className="grid grid-cols-1 gap-2">
                {DOMAINS.map(d=>(
                  <button key={d.id} onClick={()=>setDomain(d)}
                    className={`flex items-center gap-4 p-4 rounded-2xl border-2 text-right transition-all ${domain?.id===d.id?'shadow-sm':'border-gray-100 hover:border-gray-200'}`}
                    style={domain?.id===d.id?{borderColor:d.color,background:d.light}:{} }>
                    <span className="text-3xl">{d.icon}</span>
                    <div className="flex-1">
                      <p className="font-bold text-gray-900">{lang==='en'?(d.nameEn||d.nameAr):d.nameAr}</p>
                      <p className="text-xs text-gray-400 mt-0.5">{TYPES_BY_DOMAIN[d.id]?.map(t=>t.nameAr).join(' · ')}</p>
                    </div>
                    {domain?.id===d.id&&<span className="text-2xl">✓</span>}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Step 1: Type */}
          {step===1&&domain&&(
            <div>
              <p className="text-sm font-bold text-gray-700 mb-3">
                نوع السجل في <span style={{color:domain.color}}>{lang==='en'?(domain.nameEn||domain.nameAr):domain.nameAr}</span>
                <span className="text-red-400 mr-1">*</span>
              </p>
              <div className="grid grid-cols-2 gap-3">
                {TYPES_BY_DOMAIN[domain.id].map(t=>(
                  <button key={t.id} onClick={()=>setRtype(t)}
                    className={`flex items-center gap-3 p-4 rounded-2xl border-2 text-right transition-all ${rtype?.id===t.id?'shadow-sm':'border-gray-100 hover:border-gray-200'}`}
                    style={rtype?.id===t.id?{borderColor:domain.color,background:domain.light}:{}}>
                    <span className="text-2xl">{t.icon}</span>
                    <p className="font-bold text-gray-800 flex-1">{t.nameAr}</p>
                    {rtype?.id===t.id&&<span className="text-sm" style={{color:domain.color}}>✓</span>}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Step 2: Core fields */}
          {step===2&&(
            <div className="space-y-3">
              <div className="flex items-center gap-2 mb-4 p-3 rounded-xl" style={{background:domain?.light||'#f8fafc'}}>
                <span className="text-xl">{rtype?.icon}</span>
                <div>
                  <p className="text-xs font-bold" style={{color:domain?.color}}>{domain?.nameAr}</p>
                  <p className="text-xs text-gray-500">{rtype?.nameAr}</p>
                </div>
              </div>
              {CORE_FIELDS.map(f=>(
                <div key={f.key}>
                  <label className="block text-xs font-bold text-gray-600 mb-1">
                    {f.label}{f.required&&<span className="text-red-400 mr-1">*</span>}
                  </label>
                  <FieldInput field={f} value={values[f.key]} onChange={v=>set(f.key,v)}/>
                </div>
              ))}
            </div>
          )}

          {/* Step 3: Domain-specific fields */}
          {step===3&&(
            <div className="space-y-3">
              <div className="flex items-center gap-2 p-3 rounded-xl mb-2" style={{background:domain?.light}}>
                <span>{domain?.icon}</span>
                <p className="text-xs font-bold" style={{color:domain?.color}}>
                  حقول خاصة بـ: {domain?.nameAr} — {rtype?.nameAr}
                </p>
              </div>
              {domainFields.length===0&&(
                <div className="text-center py-8 text-gray-400">
                  <div className="text-3xl mb-2">✅</div>
                  <p>لا توجد حقول إضافية لهذا النوع</p>
                </div>
              )}
              {domainFields.map(f=>(
                <div key={f.key}>
                  <label className="block text-xs font-bold text-gray-600 mb-1">
                    {f.labelAr}{f.required&&<span className="text-red-400 mr-1">*</span>}
                  </label>
                  <FieldInput field={f} value={values[f.key]} onChange={v=>set(f.key,v)}/>
                </div>
              ))}
            </div>
          )}

          {/* Step 4: Attachments */}
          {step===4&&(
            <div>
              <div
                onClick={()=>fileRef.current?.click()}
                className="border-2 border-dashed border-gray-200 rounded-2xl p-8 text-center cursor-pointer hover:border-blue-300 hover:bg-blue-50/30 transition-all mb-4">
                <input ref={fileRef} type="file" multiple className="hidden"
                  onChange={e=>setFiles(p=>[...p,...Array.from(e.target.files||[])])}/>
                <div className="text-4xl mb-2">📎</div>
                <p className="font-semibold text-gray-700">أضف المرفقات</p>
                <p className="text-xs text-gray-400 mt-1">PDF, DOCX, XLSX, صور — حتى 50MB لكل ملف</p>
              </div>
              {files.length > 0 && (
                <div className="space-y-2">
                  {files.map((f,i)=>(
                    <div key={i} className="flex items-center gap-3 p-3 bg-gray-50 rounded-xl border border-gray-100">
                      <span className="text-lg">📄</span>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-gray-800 truncate">{f.name}</p>
                        <p className="text-xs text-gray-400">{(f.size/1024).toFixed(1)} KB</p>
                      </div>
                      <button onClick={()=>setFiles(p=>p.filter((_,j)=>j!==i))}
                        className="text-gray-300 hover:text-red-500 transition-colors">✕</button>
                    </div>
                  ))}
                  <p className="text-xs text-green-600 font-medium">✅ {files.length} ملف جاهز للرفع</p>
                </div>
              )}
              {files.length===0&&(
                <p className="text-center text-xs text-gray-400">يمكن إضافة المرفقات لاحقاً</p>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-gray-100 flex gap-3">
          {step>0&&(
            <button onClick={()=>setStep(s=>s-1)} className="border border-gray-200 text-gray-600 px-4 py-2.5 rounded-xl text-sm hover:bg-gray-50">
              ← السابق
            </button>
          )}
          {step<STEPS.length-1?(
            <button onClick={()=>canNext[step]&&setStep(s=>s+1)} disabled={!canNext[step]}
              className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-40 transition-colors">
              التالي →
            </button>
          ):(
            <button onClick={handleSubmit} disabled={loading}
              className="flex-1 bg-green-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-green-700 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors">
              {loading?'⏳ جارٍ الحفظ...':'✅ حفظ السجل'}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

// ─── Record Detail Panel ──────────────────────────────────────────────────────
function RecordDetail({record, onClose, onStatusChange}) {
  const domain = DOMAINS.find(d=>d.id===record.domainId)||DOMAINS[0]
  const types  = TYPES_BY_DOMAIN[record.domainId]||[]
  const rtype  = types.find(t=>t.id===record.typeId)||types[0]||{}
  const fields = DOMAIN_FIELDS[record.domainId]||[]
  const meta   = record.metadata||{}
  const statusCfg = STATUS_CFG[record.status]||STATUS_CFG['مسودة']
  const secCfg = SECURITY_CFG[record.security]||SECURITY_CFG['داخلي']

  return (
    <div className="bg-white border-r border-gray-100 flex flex-col overflow-hidden" style={{width:400,minWidth:400}}>
      {/* Header */}
      <div className="p-4 flex-shrink-0" style={{background:domain.light,borderBottom:'1px solid '+domain.border}}>
        <div className="flex items-start justify-between mb-2">
          <div className="flex items-center gap-2.5">
            <div className="w-10 h-10 rounded-xl flex items-center justify-center text-xl text-white flex-shrink-0"
              style={{background:domain.color}}>
              {rtype.icon||domain.icon}
            </div>
            <div>
              <p className="font-black text-gray-900 text-sm leading-snug">{record.titleAr}</p>
              <p className="text-[10px] font-mono text-gray-500 mt-0.5">{record.recordNumber}</p>
            </div>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-lg">✕</button>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <span className={`inline-flex items-center gap-1 text-[11px] px-2 py-0.5 rounded-full font-medium ${statusCfg.cls}`}>
            <span className={`w-1.5 h-1.5 rounded-full ${statusCfg.dot}`}/>{record.status}
          </span>
          <span className={`inline-flex items-center gap-1 text-[11px] px-2 py-0.5 rounded-full font-medium border ${secCfg.cls}`}>
            {secCfg.icon} {record.security}
          </span>
          <span className="text-[11px] px-2 py-0.5 rounded-full font-medium text-white" style={{background:domain.color}}>
            {lang==='en'?(rtype.nameEn||rtype.nameAr):rtype.nameAr}
          </span>
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* Core info */}
        <div className="space-y-2 text-xs">
          {[
            ['الإدارة',         record.dept],
            ['تاريخ الإنشاء',   record.createdAt],
            ['تاريخ الوثيقة',   meta.document_date||'—'],
            ['الكلمات المفتاحية',meta.tags||'—'],
          ].map(([l,v])=> v && v!=='—' ? (
            <div key={l} className="flex justify-between items-start gap-2 py-1.5 border-b border-gray-50">
              <span className="text-gray-400 flex-shrink-0">{l}</span>
              <span className="font-medium text-gray-800 text-right">{v}</span>
            </div>
          ):null)}
        </div>

        {/* Domain metadata */}
        {fields.length>0&&(
          <div>
            <p className="text-[11px] font-black text-gray-500 mb-2 uppercase tracking-wider">بيانات {lang==='en'?(domain.nameEn||domain.nameAr):domain.nameAr}</p>
            <div className="space-y-1.5 text-xs">
              {fields.map(f=>{
                const v = meta[f.key]
                if (!v) return null
                return (
                  <div key={f.key} className="flex justify-between items-start gap-2 py-1.5 border-b border-gray-50">
                    <span className="text-gray-400">{f.labelAr}</span>
                    <span className="font-medium text-gray-800 text-right">
                      {f.key.includes('value')||f.key==='amount'
                        ? Number(v).toLocaleString('ar-SA')+' '+(meta.currency||meta.currency_fin||'')
                        : v}
                    </span>
                  </div>
                )
              })}
            </div>
          </div>
        )}

        {/* Description */}
        {meta.description&&(
          <div>
            <p className="text-[11px] font-black text-gray-500 mb-1">الوصف</p>
            <p className="text-xs text-gray-600 leading-relaxed">{meta.description}</p>
          </div>
        )}

        {/* Attachments */}
        {record.attachments?.length>0&&(
          <div>
            <p className="text-[11px] font-black text-gray-500 mb-2">المرفقات ({record.attachments.length})</p>
            <div className="space-y-1.5">
              {record.attachments.map((a,i)=>(
                <div key={i} className="flex items-center gap-2 p-2 bg-gray-50 rounded-xl">
                  <span>📄</span>
                  <span className="text-xs font-medium text-gray-700 truncate flex-1">{a.name||a}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Actions */}
      <div className="p-4 border-t border-gray-100 space-y-2 flex-shrink-0">
        {record.status==='مسودة'&&(
          <button onClick={()=>onStatusChange(record,'قيد المراجعة')}
            className="w-full bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 transition-colors">
            📤 إرسال للمراجعة
          </button>
        )}
        {record.status==='قيد المراجعة'&&(
          <div className="grid grid-cols-2 gap-2">
            <button onClick={()=>onStatusChange(record,'معتمد')}
              className="bg-green-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-green-700">✅ اعتماد</button>
            <button onClick={()=>onStatusChange(record,'مرفوض')}
              className="bg-red-600 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-red-700">❌ رفض</button>
          </div>
        )}
        {record.status==='معتمد'&&(
          <button onClick={()=>onStatusChange(record,'مؤرشف')}
            className="w-full border-2 border-gray-200 text-gray-600 py-2.5 rounded-xl text-sm font-bold hover:bg-gray-50">
            🗃️ أرشفة
          </button>
        )}
      </div>
    </div>
  )
}

// ─── Main Page ─────────────────────────────────────────────────────────────────
export default function RecordsPage() {
  const { lang, setLang, t, isRTL, fmtDate, fmtNum } = useLang()
  const {show,ToastContainer} = useToast()
  const [records,setRecords]       = useLocalStorage('ecm_records', MOCK_RECORDS)
  const [selected,setSelected]     = useState(null)
  const [showCreate,setShowCreate] = useState(false)
  const [filterDomain,setFilter]   = useState('all')
  const [filterStatus,setFilterS]  = useState('all')
  const [search,setSearch]         = useState('')
  const [view,setView]             = useState('list') // list|grid

  useEffect(()=>{
    client.get('/api/v1/records').then(r=>{
      const d = r.data?.data?.items||r.data?.data
      if(Array.isArray(d)&&d.length>0){
        setRecords(d.map(rec=>({
          id:rec.recordId, recordNumber:rec.recordNumber,
          domainId:rec.domainId, typeId:rec.typeId,
          titleAr:rec.titleAr, status:'مسودة',
          security:rec.securityLevel||'داخلي', dept:rec.department,
          createdAt:rec.createdAt, metadata:JSON.parse(rec.metadataJson||'{}'),
        })))
      }
    }).catch(()=>{})
  },[])

  const safeRecords = Array.isArray(records) ? records : MOCK_RECORDS

  const filtered = safeRecords.filter(r=>
    (filterDomain==='all'||String(r.domainId)===String(filterDomain))&&
    (filterStatus==='all'||r.status===filterStatus)&&
    (!search||(r.titleAr||'').includes(search)||(r.recordNumber||'').includes(search)||(r.dept||'').includes(search))
  )

  const stats = {
    total:   safeRecords.length,
    draft:   safeRecords.filter(r=>r.status==='مسودة').length,
    review:  safeRecords.filter(r=>r.status==='قيد المراجعة').length,
    approved:safeRecords.filter(r=>r.status==='معتمد').length,
  }

  const handleStatusChange = (record, newStatus) => {
    setRecords(p=>(Array.isArray(p)?p:MOCK_RECORDS).map(r=>r.id===record.id?{...r,status:newStatus}:r))
    client.post(`/api/v1/records/${record.id}/submit`).catch(()=>{})
    show(`✅ تم تغيير حالة "${record.titleAr}" إلى ${newStatus}`, 'success')
    if(selected?.id===record.id) setSelected(p=>({...p,status:newStatus}))
  }

  return (
    <div className="flex flex-col h-full">
      <ToastContainer/>
      {showCreate&&(
        <CreateRecordModal
          onClose={()=>setShowCreate(false)}
          onSuccess={rec=>{
            setRecords(p=>[rec,...(Array.isArray(p)?p:MOCK_RECORDS)])
            show(`✅ تم إنشاء السجل: ${rec.titleAr}`, 'success')
            setSelected(rec)
          }}
        />
      )}

      {/* ── Header ── */}
      <div className="flex-shrink-0 space-y-4 mb-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-black text-gray-900">t('records_title')</h1>
            <p className="text-sm text-gray-400 mt-0.5">نظام السجلات المبني على البيانات الوصفية</p>
          </div>
          <button onClick={()=>setShowCreate(true)}
            className="bg-blue-700 text-white px-5 py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 flex items-center gap-2 shadow-sm transition-colors">
            + سجل جديد
          </button>
        </div>

        {/* KPI cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {[
            {l:'إجمالي السجلات', v:stats.total,    icon:'📦', cls:'bg-indigo-50 text-indigo-700 border-indigo-100'},
            {l:'مسودة',          v:stats.draft,    icon:'📝', cls:'bg-gray-50 text-gray-700 border-gray-200'},
            {l:'قيد المراجعة',   v:stats.review,   icon:'🔄', cls:'bg-amber-50 text-amber-700 border-amber-100'},
            {l:'معتمد',          v:stats.approved, icon:'✅', cls:'bg-green-50 text-green-700 border-green-100'},
          ].map(k=>(
            <div key={k.l} className={`${k.cls} border rounded-2xl p-4 flex items-center gap-3`}>
              <span className="text-2xl">{k.icon}</span>
              <div><p className="text-2xl font-black">{k.v}</p><p className="text-xs opacity-80">{k.l}</p></div>
            </div>
          ))}
        </div>

        {/* Domain pills */}
        <div className="flex gap-2 flex-wrap">
          <button onClick={()=>setFilter('all')}
            className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all ${filterDomain==='all'?'bg-gray-900 text-white':'bg-white border border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
            الكل ({safeRecords.length})
          </button>
          {DOMAINS.map(d=>{
            const cnt = safeRecords.filter(r=>r.domainId===d.id).length
            return (
              <button key={d.id} onClick={()=>setFilter(d.id)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-bold transition-all border ${String(filterDomain)===String(d.id)?'text-white border-transparent':'bg-white text-gray-600 hover:bg-gray-50'}`}
                style={String(filterDomain)===String(d.id)?{background:d.color,borderColor:d.color}:{borderColor:d.border}}>
                {d.icon} {lang==='en'?(d.nameEn||d.nameAr):d.nameAr} <span className="opacity-70">({cnt})</span>
              </button>
            )
          })}
        </div>

        {/* Search + filters */}
        <div className="bg-white rounded-2xl border border-gray-100 p-3 flex gap-2 flex-wrap">
          <div className="relative flex-1 min-w-40">
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
            <input value={search} onChange={e=>setSearch(e.target.value)}
              placeholder={t('search')+' ...'}
              className="w-full pr-9 pl-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
          </div>
          <select value={filterStatus} onChange={e=>setFilterS(e.target.value)}
            className="border border-gray-200 rounded-xl px-3 py-2 text-sm text-gray-600 focus:outline-none">
            <option value="all">كل الحالات</option>
            {Object.keys(STATUS_CFG).map(s=><option key={s}>{s}</option>)}
          </select>
          <div className="flex border border-gray-200 rounded-xl overflow-hidden">
            <button onClick={()=>setView('list')} className={`px-3 py-2 text-sm ${view==='list'?'bg-gray-900 text-white':'text-gray-500 hover:bg-gray-50'}`}>☰</button>
            <button onClick={()=>setView('grid')} className={`px-3 py-2 text-sm ${view==='grid'?'bg-gray-900 text-white':'text-gray-500 hover:bg-gray-50'}`}>⊞</button>
          </div>
        </div>
      </div>

      {/* ── Content ── */}
      <div className="flex-1 flex gap-4 overflow-hidden min-h-0">
        <div className={`flex-1 overflow-y-auto ${selected?'hidden lg:block':''}`}>
          {filtered.length===0&&(
            <div className="bg-white rounded-2xl border border-gray-100 p-16 text-center">
              <div className="text-5xl mb-3">📭</div>
              <p className="font-semibold text-gray-600">لا توجد سجلات</p>
              <button onClick={()=>setShowCreate(true)}
                className="mt-4 bg-blue-700 text-white px-5 py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800">
                + أنشئ أول سجل
              </button>
            </div>
          )}

          {view==='list'&&filtered.length>0&&(
            <div className="bg-white rounded-2xl border border-gray-100 overflow-hidden shadow-sm">
              <table className="w-full text-sm">
                <thead><tr className="bg-gray-50 border-b border-gray-100">
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400">السجل</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">النطاق</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">الإدارة</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400">الحالة</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden lg:table-cell">السرية</th>
                </tr></thead>
                <tbody className="divide-y divide-gray-50">
                  {filtered.map(r=>{
                    const d = DOMAINS.find(x=>x.id===r.domainId)||DOMAINS[0]
                    const s = STATUS_CFG[r.status]||STATUS_CFG['مسودة']
                    const sec = SECURITY_CFG[r.security]||SECURITY_CFG['داخلي']
                    return (
                      <tr key={r.id} onClick={()=>setSelected(selected?.id===r.id?null:r)}
                        className={`cursor-pointer transition-colors ${selected?.id===r.id?'bg-blue-50':'hover:bg-gray-50'}`}>
                        <td className="px-4 py-3">
                          <p className="font-bold text-gray-900 truncate max-w-[220px]">{r.titleAr}</p>
                          <p className="text-[10px] font-mono text-gray-400 mt-0.5">{r.recordNumber}</p>
                        </td>
                        <td className="px-4 py-3 hidden md:table-cell">
                          <span className="inline-flex items-center gap-1.5 text-[11px] px-2.5 py-1 rounded-full font-bold text-white" style={{background:d.color}}>
                            {d.icon} {lang==='en'?(d.nameEn||d.nameAr):d.nameAr}
                          </span>
                        </td>
                        <td className="px-4 py-3 hidden md:table-cell text-xs text-gray-500">{r.dept||'—'}</td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center gap-1 text-[11px] px-2 py-0.5 rounded-full font-medium ${s.cls}`}>
                            <span className={`w-1.5 h-1.5 rounded-full ${s.dot}`}/>{r.status}
                          </span>
                        </td>
                        <td className="px-4 py-3 hidden lg:table-cell">
                          <span className={`text-[11px] px-2 py-0.5 rounded-full font-medium border ${sec.cls}`}>
                            {sec.icon} {r.security}
                          </span>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}

          {view==='grid'&&filtered.length>0&&(
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {filtered.map(r=>{
                const d = DOMAINS.find(x=>x.id===r.domainId)||DOMAINS[0]
                const types = TYPES_BY_DOMAIN[r.domainId]||[]
                const t = types.find(x=>x.id===r.typeId)||types[0]||{}
                const s = STATUS_CFG[r.status]||STATUS_CFG['مسودة']
                return (
                  <div key={r.id} onClick={()=>setSelected(selected?.id===r.id?null:r)}
                    className={`bg-white rounded-2xl border-2 p-4 cursor-pointer hover:shadow-md transition-all ${selected?.id===r.id?'shadow-md':''}`}
                    style={{borderColor:selected?.id===r.id?d.color:d.border}}>
                    <div className="flex items-start justify-between mb-3">
                      <div className="w-10 h-10 rounded-xl flex items-center justify-center text-xl text-white flex-shrink-0"
                        style={{background:d.color}}>{t.icon||d.icon}</div>
                      <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium flex items-center gap-1 ${s.cls}`}>
                        <span className={`w-1.5 h-1.5 rounded-full ${s.dot}`}/>{r.status}
                      </span>
                    </div>
                    <p className="font-bold text-gray-900 text-sm mb-1 line-clamp-2">{r.titleAr}</p>
                    <p className="text-[10px] font-mono text-gray-400 mb-2">{r.recordNumber}</p>
                    <div className="flex items-center justify-between">
                      <span className="text-[10px] font-bold text-white px-2 py-0.5 rounded-full" style={{background:d.color}}>
                        {t.nameAr||d.nameAr}
                      </span>
                      <span className="text-[10px] text-gray-400">{r.dept}</span>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>

        {/* Detail panel */}
        {selected&&(
          <RecordDetail
            record={selected}
            onClose={()=>setSelected(null)}
            onStatusChange={handleStatusChange}
          />
        )}
      </div>
    </div>
  )
}
