import React, { useState, useEffect } from 'react'
import client from '../../api/client'
import { useToast } from '../../components/Toast'

// ─── Static schema (mirrors backend) ─────────────────────────────────────────
const ASSET_TYPES = [
  { code:'KnowledgeAsset',       nameAr:'أصل معرفي (قاعدة)',   nameEn:'Knowledge Asset',     icon:'📦', color:'bg-indigo-500',  border:'border-indigo-200',  light:'bg-indigo-50' },
  { code:'Book',                 nameAr:'كتاب',                 nameEn:'Book',                icon:'📗', color:'bg-green-600',   border:'border-green-200',   light:'bg-green-50' },
  { code:'Manuscript',           nameAr:'مخطوطة',               nameEn:'Manuscript',          icon:'📜', color:'bg-amber-700',   border:'border-amber-200',   light:'bg-amber-50' },
  { code:'HistoricalDocument',   nameAr:'وثيقة تاريخية',        nameEn:'Historical Document', icon:'📋', color:'bg-purple-600',  border:'border-purple-200',  light:'bg-purple-50' },
  { code:'ImageAsset',           nameAr:'صورة / مادة مرئية',    nameEn:'Image Asset',         icon:'🖼', color:'bg-pink-600',    border:'border-pink-200',    light:'bg-pink-50' },
  { code:'MapAsset',             nameAr:'خريطة',                nameEn:'Map',                 icon:'🗺', color:'bg-cyan-600',    border:'border-cyan-200',    light:'bg-cyan-50' },
  { code:'ResearchPaper',        nameAr:'بحث / دراسة',          nameEn:'Research Paper',      icon:'🔬', color:'bg-emerald-600', border:'border-emerald-200', light:'bg-emerald-50' },
  { code:'AudioVisualAsset',     nameAr:'مادة صوتية / مرئية',   nameEn:'Audio-Visual',        icon:'🎬', color:'bg-red-600',     border:'border-red-200',     light:'bg-red-50' },
  { code:'AdministrativeRecord', nameAr:'سجل إداري',            nameEn:'Administrative Record',icon:'🗃', color:'bg-blue-600',   border:'border-blue-200',    light:'bg-blue-50' },
  { code:'Periodical',           nameAr:'دورية / مجلة',          nameEn:'Periodical',          icon:'📰', color:'bg-yellow-600', border:'border-yellow-200',  light:'bg-yellow-50' },
  { code:'Thesis',               nameAr:'رسالة علمية',           nameEn:'Thesis',              icon:'🎓', color:'bg-violet-600',  border:'border-violet-200',  light:'bg-violet-50' },
]

const ASPECTS = [
  {
    code:'cataloging', nameAr:'الفهرسة', icon:'📚',
    desc:'البيانات الببليوغرافية الأساسية — العنوان، المؤلف، المصدر، رقم الحفظ',
    fields:[
      { f:'titleAr',         l:'العنوان بالعربية',         t:'text',     req:true },
      { f:'titleEn',         l:'العنوان بالإنجليزية',      t:'text' },
      { f:'titleAlt',        l:'العنوان البديل',            t:'text' },
      { f:'descriptionAr',   l:'الوصف',                    t:'textarea' },
      { f:'language',        l:'اللغة',                    t:'select',   opts:['ar','en','ar, en','fa','tr','أخرى'] },
      { f:'source',          l:'الجهة / المصدر',           t:'text' },
      { f:'creator',         l:'المؤلف / المنتج',          t:'text' },
      { f:'contributors',    l:'المساهمون',                t:'text' },
      { f:'publisher',       l:'الناشر',                   t:'text' },
      { f:'productionYear',  l:'سنة الإنتاج / النشر',     t:'number' },
      { f:'productionPeriod',l:'الحقبة الزمنية',           t:'text',     ph:'مثال: 1350-1380 هـ' },
      { f:'edition',         l:'الطبعة',                   t:'text' },
      { f:'callNumber',      l:'رقم الحفظ / الاستدعاء',  t:'text' },
      { f:'pageCount',       l:'عدد الصفحات',              t:'number' },
      { f:'dimensions',      l:'الأبعاد',                  t:'text',     ph:'مثال: 25×35 سم' },
    ]
  },
  {
    code:'classification', nameAr:'التصنيف', icon:'🏷️',
    desc:'الموضوع، الكلمات المفتاحية، النطاق الجغرافي، العصر التاريخي',
    fields:[
      { f:'primarySubject',   l:'التصنيف الموضوعي الرئيسي', t:'text',   req:true },
      { f:'secondarySubjects',l:'موضوعات فرعية إضافية',      t:'text',   ph:'مفصولة بفواصل' },
      { f:'keywords',         l:'الكلمات المفتاحية',          t:'text',   ph:'كلمة1، كلمة2، ...' },
      { f:'geographicScope',  l:'النطاق الجغرافي',            t:'text' },
      { f:'temporalCoverage', l:'الحقبة التاريخية',           t:'text',   ph:'مثال: 1900-1950' },
      { f:'era',              l:'العصر',                      t:'select', opts:['ما قبل الإسلام','صدر الإسلام','الدولة السعودية الأولى','الدولة السعودية الثانية','المملكة الحديثة','المعاصر'] },
      { f:'darahCategory',    l:'تصنيف الدارة الداخلي',       t:'text' },
      { f:'tags',             l:'الوسوم',                     t:'text',   ph:'وسم1، وسم2، ...' },
    ]
  },
  {
    code:'rights', nameAr:'الحقوق والوصول', icon:'🔐',
    desc:'مستوى السرية، الترخيص، حقوق النشر، شروط الاستخدام',
    fields:[
      { f:'confidentiality',   l:'مستوى السرية',         t:'select', req:true, opts:['عام','داخلي','سري','سري للغاية'] },
      { f:'license',           l:'الترخيص',              t:'select', opts:['CC BY','CC BY-NC','CC BY-SA','ملكية خاصة','نطاق عام','جميع الحقوق محفوظة'] },
      { f:'copyright',         l:'حقوق النشر',           t:'text' },
      { f:'rightsStatement',   l:'بيان الحقوق',          t:'textarea' },
      { f:'isPublicAccess',    l:'متاح للعموم',          t:'boolean' },
      { f:'isDownloadable',    l:'قابل للتنزيل',         t:'boolean' },
      { f:'accessRestrictions',l:'قيود الوصول',          t:'textarea' },
      { f:'useConditions',     l:'شروط الاستخدام',       t:'textarea' },
    ]
  },
  {
    code:'retention', nameAr:'الحفظ والاستبقاء', icon:'🗃️',
    desc:'جدول الاحتفاظ، تاريخ الانتهاء، الحجز القانوني، أسلوب الإتلاف',
    fields:[
      { f:'retention',         l:'جدول الاستبقاء',       t:'select', req:true, opts:['دائم','25 سنة','10 سنوات','7 سنوات','5 سنوات','3 سنوات'] },
      { f:'retentionExpiry',   l:'تاريخ انتهاء الاحتفاظ',t:'date' },
      { f:'isLegalHold',       l:'حجز قانوني',           t:'boolean' },
      { f:'disposalMethod',    l:'أسلوب الإتلاف',        t:'select', opts:['لا ينطبق','إتلاف آمن','نقل للأرشيف الوطني','مراجعة قبل الإتلاف'] },
      { f:'archivalReference', l:'المرجع الأرشيفي',      t:'text' },
    ]
  },
  {
    code:'digitization', nameAr:'الرقمنة', icon:'💻',
    desc:'حالة الرقمنة، جودة OCR، بيانات المسح الضوئي، الملف الأصلي',
    fields:[
      { f:'digitizationStatus',l:'حالة الرقمنة',         t:'select', req:true, opts:['لم يُرقَّم','قيد الرقمنة','مُرقَّم','تم فحص الجودة','منشور رقمياً'] },
      { f:'ocrQuality',        l:'جودة OCR',             t:'select', opts:['لا يوجد','منخفضة < 70%','متوسطة 70-85%','عالية 85-95%','ممتازة > 95%'] },
      { f:'ocrConfidence',     l:'دقة OCR (%)',          t:'number',  ph:'0 - 100' },
      { f:'scannerModel',      l:'طراز الماسح',          t:'text' },
      { f:'resolution',        l:'الدقة (DPI)',           t:'number' },
      { f:'colorProfile',      l:'ملف اللون',            t:'select', opts:['24-bit RGB','Grayscale 8-bit','1-bit Bitonal','CMYK'] },
      { f:'masterFileFormat',  l:'تنسيق الملف الأصلي',  t:'select', opts:['TIFF','RAW','PDF/A','JPEG 2000','PNG','MP4','WAV','FLAC'] },
      { f:'digitizedBy',       l:'جهة الرقمنة',          t:'text' },
      { f:'digitizedAt',       l:'تاريخ الرقمنة',        t:'date' },
    ]
  },
  {
    code:'review', nameAr:'المراجعة والجودة', icon:'✅',
    desc:'حالة الاعتماد، درجة الجودة، المراجع، توقيت المراجعة',
    fields:[
      { f:'status',       l:'حالة الأصل',     t:'select', req:true, opts:['مسودة','قيد المراجعة','معتمد','منشور','مقيد','مؤرشف','مسحوب'] },
      { f:'qualityScore', l:'درجة الجودة (0-100)', t:'number' },
      { f:'statusNote',   l:'ملاحظات الحالة', t:'textarea' },
    ]
  },
]

const STATUS_CFG = {
  'مسودة':        { cls:'bg-gray-100 text-gray-600',    dot:'bg-gray-400' },
  'قيد المراجعة': { cls:'bg-yellow-100 text-yellow-700',dot:'bg-yellow-500' },
  'معتمد':        { cls:'bg-green-100 text-green-700',  dot:'bg-green-500' },
  'منشور':        { cls:'bg-blue-100 text-blue-700',    dot:'bg-blue-500' },
  'مقيد':         { cls:'bg-orange-100 text-orange-700',dot:'bg-orange-500' },
  'مؤرشف':        { cls:'bg-gray-100 text-gray-500',    dot:'bg-gray-400' },
}

const DIGIT_CFG = {
  'لم يُرقَّم':      { pct:0,   cls:'bg-gray-200' },
  'قيد الرقمنة':    { pct:25,  cls:'bg-yellow-400' },
  'مُرقَّم':         { pct:60,  cls:'bg-blue-400' },
  'تم فحص الجودة': { pct:85,  cls:'bg-indigo-500' },
  'منشور رقمياً':   { pct:100, cls:'bg-green-500' },
}

// ─── Mock assets ─────────────────────────────────────────────────────────────
const MOCK_ASSETS = [
  { id:1, assetCode:'DARAH-MS-1401-12345', type:'Manuscript',        titleAr:'مخطوطة كتاب الأمثال العربية',       status:'معتمد',        digitizationStatus:'منشور رقمياً',   confidentiality:'عام',     productionPeriod:'1250-1300م', primarySubject:'الأدب العربي' },
  { id:2, assetCode:'DARAH-BK-2024-67890', type:'Book',              titleAr:'تاريخ نجد في القرن العشرين',        status:'منشور',        digitizationStatus:'مُرقَّم',         confidentiality:'عام',     productionYear:1985,          primarySubject:'التاريخ السعودي' },
  { id:3, assetCode:'DARAH-IM-2025-11111', type:'ImageAsset',        titleAr:'صور الرياض التاريخية 1940-1960',    status:'قيد المراجعة', digitizationStatus:'تم فحص الجودة',  confidentiality:'داخلي',   productionPeriod:'1940-1960',  primarySubject:'الصور التاريخية' },
  { id:4, assetCode:'DARAH-HD-2025-22222', type:'HistoricalDocument',titleAr:'وثيقة تأسيس الدارة ١٩٧٢',          status:'معتمد',        digitizationStatus:'منشور رقمياً',   confidentiality:'عام',     productionYear:1972,          primarySubject:'الوثائق الرسمية' },
  { id:5, assetCode:'DARAH-RP-2026-33333', type:'ResearchPaper',     titleAr:'العمارة النجدية التقليدية',          status:'مسودة',        digitizationStatus:'لم يُرقَّم',     confidentiality:'داخلي',   productionYear:2026,          primarySubject:'التراث المعماري' },
  { id:6, assetCode:'DARAH-MP-2024-44444', type:'MapAsset',          titleAr:'خرائط شبه الجزيرة العربية 1900',   status:'معتمد',        digitizationStatus:'تم فحص الجودة', confidentiality:'عام',     productionPeriod:'1900-1920',  primarySubject:'الجغرافيا التاريخية' },
  { id:7, assetCode:'DARAH-AV-2025-55555', type:'AudioVisualAsset',  titleAr:'تسجيلات الذاكرة الشفهية',          status:'قيد المراجعة', digitizationStatus:'قيد الرقمنة',    confidentiality:'داخلي',   productionYear:2024,          primarySubject:'التراث الشفهي' },
]

// ─── Field renderer ───────────────────────────────────────────────────────────
function FieldInput({ field, value, onChange }) {
  const base = "w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right bg-white"

  if (field.t === 'select') return (
    <select value={value||''} onChange={e=>onChange(e.target.value)} className={base}>
      <option value="">— اختر —</option>
      {field.opts?.map(o => <option key={o}>{o}</option>)}
    </select>
  )
  if (field.t === 'textarea') return (
    <textarea value={value||''} onChange={e=>onChange(e.target.value)} rows={3}
      placeholder={field.ph||''} className={`${base} resize-none`} />
  )
  if (field.t === 'boolean') return (
    <div className="flex items-center gap-3 py-1">
      <button onClick={() => onChange(!value)}
        className={`w-11 h-6 rounded-full transition-all flex items-center px-0.5 ${value ? 'bg-blue-600' : 'bg-gray-200'}`}>
        <div className={`w-5 h-5 bg-white rounded-full shadow transition-transform ${value ? 'translate-x-5' : 'translate-x-0'}`}/>
      </button>
      <span className="text-sm text-gray-600">{value ? 'نعم' : 'لا'}</span>
    </div>
  )
  if (field.t === 'number') return (
    <input type="number" value={value||''} onChange={e=>onChange(e.target.value)}
      placeholder={field.ph||''} className={base} dir="ltr"/>
  )
  if (field.t === 'date') return (
    <input type="date" value={value||''} onChange={e=>onChange(e.target.value)} className={base} dir="ltr"/>
  )
  return (
    <input type="text" value={value||''} onChange={e=>onChange(e.target.value)}
      placeholder={field.ph||''} className={base}/>
  )
}

// ─── Asset Card ───────────────────────────────────────────────────────────────
function AssetCard({ asset, onClick }) {
  const typeInfo = ASSET_TYPES.find(t => t.code === asset.type) || ASSET_TYPES[0]
  const statusCfg = STATUS_CFG[asset.status] || { cls:'bg-gray-100 text-gray-600', dot:'bg-gray-400' }
  const digitCfg = DIGIT_CFG[asset.digitizationStatus] || { pct:0, cls:'bg-gray-200' }

  return (
    <div onClick={onClick}
      className={`bg-white rounded-2xl border-2 ${typeInfo.border} p-4 cursor-pointer hover:shadow-md transition-all group`}>
      <div className="flex items-start gap-3 mb-3">
        <div className={`w-10 h-10 ${typeInfo.color} rounded-xl flex items-center justify-center text-xl text-white flex-shrink-0`}>
          {typeInfo.icon}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-bold text-gray-900 text-sm leading-snug truncate">{asset.titleAr}</p>
          <p className="text-[10px] text-gray-400 font-mono mt-0.5">{asset.assetCode}</p>
        </div>
        <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium flex-shrink-0 flex items-center gap-1 ${statusCfg.cls}`}>
          <span className={`w-1.5 h-1.5 rounded-full ${statusCfg.dot}`}/>
          {asset.status}
        </span>
      </div>

      <div className="space-y-1.5 text-xs text-gray-500">
        <div className="flex items-center justify-between">
          <span className={`${typeInfo.light} ${typeInfo.color.replace('bg-','text-').replace('-500','-700').replace('-600','-700').replace('-700','-800')} text-[10px] px-2 py-0.5 rounded-full font-semibold`}>
            {typeInfo.nameAr}
          </span>
          <span className="text-gray-400">{asset.productionYear || asset.productionPeriod || '—'}</span>
        </div>
        {asset.primarySubject && (
          <p className="text-gray-500 truncate">🏷️ {asset.primarySubject}</p>
        )}
      </div>

      {/* Digitization progress */}
      <div className="mt-3">
        <div className="flex items-center justify-between text-[10px] text-gray-400 mb-1">
          <span>الرقمنة</span>
          <span>{asset.digitizationStatus}</span>
        </div>
        <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
          <div className={`h-full rounded-full transition-all ${digitCfg.cls}`}
            style={{ width: `${digitCfg.pct}%` }}/>
        </div>
      </div>
    </div>
  )
}

// ─── New Asset Modal ──────────────────────────────────────────────────────────
function NewAssetModal({ onClose, onSuccess }) {
  const [selectedType, setSelectedType] = useState(null)
  const [step, setStep]                 = useState(1) // 1: type, 2: form
  const [values, setValues]             = useState({})
  const [activeAspect, setActiveAspect] = useState('cataloging')
  const [loading, setLoading]           = useState(false)

  const set = (f, v) => setValues(p => ({...p, [f]: v}))

  const handleCreate = async () => {
    if (!values.titleAr?.trim()) return
    setLoading(true)
    try {
      await client.post('/api/v1/content-model/assets', {
        titleAr: values.titleAr,
        type: ASSET_TYPES.findIndex(t => t.code === selectedType.code),
        titleEn: values.titleEn,
        source: values.source,
      })
    } catch {}
    const newAsset = {
      id: Date.now(),
      assetCode: `DARAH-${selectedType.code.slice(0,2).toUpperCase()}-${new Date().getFullYear()}-${Math.floor(Math.random()*90000+10000)}`,
      type: selectedType.code,
      titleAr: values.titleAr,
      status: values.status || 'مسودة',
      digitizationStatus: values.digitizationStatus || 'لم يُرقَّم',
      confidentiality: values.confidentiality || 'داخلي',
      productionYear: values.productionYear,
      primarySubject: values.primarySubject,
      ...values
    }
    setLoading(false)
    onSuccess(newAsset)
    onClose()
  }

  return (
    <div className="fixed inset-0 bg-black/60 flex items-start justify-center z-50 p-4 overflow-y-auto" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-3xl my-4" onClick={e => e.stopPropagation()}>

        {/* Header */}
        <div className="p-5 border-b border-gray-100 flex items-center justify-between">
          <div>
            <h2 className="font-black text-gray-900">إنشاء أصل معرفي جديد</h2>
            <p className="text-xs text-gray-400 mt-0.5">نموذج المحتوى المؤسسي — دارة الملك عبدالعزيز</p>
          </div>
          <button onClick={onClose} className="w-9 h-9 rounded-xl hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
        </div>

        {step === 1 && (
          <div className="p-5">
            <p className="text-sm font-bold text-gray-700 mb-3">اختر نوع الأصل <span className="text-red-400">*</span></p>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {ASSET_TYPES.filter(t => t.code !== 'KnowledgeAsset').map(type => (
                <button key={type.code} onClick={() => setSelectedType(type)}
                  className={`flex items-center gap-3 p-3.5 rounded-2xl border-2 text-right transition-all ${
                    selectedType?.code === type.code
                      ? `${type.border} ${type.light} shadow-sm`
                      : 'border-gray-100 hover:border-gray-200 hover:bg-gray-50'
                  }`}>
                  <div className={`w-9 h-9 ${type.color} rounded-xl flex items-center justify-center text-lg text-white flex-shrink-0`}>
                    {type.icon}
                  </div>
                  <div className="min-w-0">
                    <p className={`text-sm font-bold truncate ${selectedType?.code===type.code ? type.color.replace('bg-','text-') : 'text-gray-700'}`}>
                      {type.nameAr}
                    </p>
                    <p className="text-[10px] text-gray-400">{type.nameEn}</p>
                  </div>
                </button>
              ))}
            </div>
            <div className="flex justify-end mt-5">
              <button onClick={() => selectedType && setStep(2)} disabled={!selectedType}
                className="bg-blue-700 text-white px-6 py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-40 transition-colors">
                التالي: إدخال البيانات →
              </button>
            </div>
          </div>
        )}

        {step === 2 && selectedType && (
          <div className="flex h-[70vh]">
            {/* Aspects sidebar */}
            <div className="w-44 border-l border-gray-100 flex flex-col flex-shrink-0 bg-gray-50">
              <div className={`p-3 ${selectedType.light} border-b border-gray-100`}>
                <div className={`w-8 h-8 ${selectedType.color} rounded-xl flex items-center justify-center text-lg text-white mb-1`}>{selectedType.icon}</div>
                <p className="text-xs font-bold text-gray-700">{selectedType.nameAr}</p>
              </div>
              {ASPECTS.map(asp => (
                <button key={asp.code} onClick={() => setActiveAspect(asp.code)}
                  className={`text-right px-3 py-2.5 text-xs font-medium transition-colors border-b border-gray-100 flex items-center gap-2 ${
                    activeAspect===asp.code ? 'bg-white text-blue-700 font-bold border-l-2 border-l-blue-600' : 'text-gray-600 hover:bg-white'
                  }`}>
                  <span>{asp.icon}</span>
                  <span className="truncate">{asp.nameAr}</span>
                </button>
              ))}
            </div>

            {/* Fields area */}
            <div className="flex-1 overflow-y-auto p-5">
              {ASPECTS.filter(a => a.code === activeAspect).map(asp => (
                <div key={asp.code}>
                  <div className="flex items-center gap-2 mb-4">
                    <span className="text-xl">{asp.icon}</span>
                    <div>
                      <p className="font-bold text-gray-900 text-sm">{asp.nameAr}</p>
                      <p className="text-xs text-gray-400">{asp.desc}</p>
                    </div>
                  </div>
                  <div className="space-y-3">
                    {asp.fields.map(field => (
                      <div key={field.f}>
                        <label className="block text-xs font-semibold text-gray-600 mb-1">
                          {field.l}
                          {field.req && <span className="text-red-400 mr-1">*</span>}
                        </label>
                        <FieldInput field={field} value={values[field.f]} onChange={v => set(field.f, v)} />
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="p-4 border-t border-gray-100 flex gap-3">
            <button onClick={() => setStep(1)} className="border border-gray-200 text-gray-600 px-4 py-2.5 rounded-xl text-sm hover:bg-gray-50">← السابق</button>
            <button onClick={handleCreate} disabled={loading || !values.titleAr}
              className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-50 flex items-center justify-center gap-2 transition-colors">
              {loading ? '⏳' : '✅'} {loading ? 'جارٍ الحفظ...' : 'حفظ الأصل المعرفي'}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}

// ─── Asset Detail Panel ───────────────────────────────────────────────────────
function AssetDetailPanel({ asset, onClose }) {
  const typeInfo = ASSET_TYPES.find(t => t.code === asset.type) || ASSET_TYPES[0]
  const statusCfg = STATUS_CFG[asset.status] || { cls:'bg-gray-100 text-gray-600', dot:'bg-gray-400' }
  const digitCfg = DIGIT_CFG[asset.digitizationStatus] || { pct:0, cls:'bg-gray-200' }
  const [activeAsp, setActiveAsp] = useState('cataloging')

  const fields = {
    cataloging:     ['titleAr','titleEn','descriptionAr','language','source','creator','productionYear','productionPeriod','callNumber','pageCount'],
    classification: ['primarySubject','secondarySubjects','keywords','geographicScope','temporalCoverage','era','tags'],
    rights:         ['confidentiality','license','copyright','isPublicAccess','isDownloadable'],
    retention:      ['retention','isLegalHold','archivalReference'],
    digitization:   ['digitizationStatus','ocrQuality','ocrConfidence','resolution','masterFileFormat','digitizedBy'],
    review:         ['status','qualityScore','statusNote'],
  }

  const labels = {
    titleAr:'العنوان بالعربية', titleEn:'العنوان بالإنجليزية', descriptionAr:'الوصف',
    language:'اللغة', source:'المصدر', creator:'المؤلف', productionYear:'سنة الإنتاج',
    productionPeriod:'الحقبة الزمنية', callNumber:'رقم الحفظ', pageCount:'الصفحات',
    primarySubject:'الموضوع الرئيسي', secondarySubjects:'موضوعات فرعية', keywords:'الكلمات المفتاحية',
    geographicScope:'النطاق الجغرافي', temporalCoverage:'الحقبة التاريخية', era:'العصر', tags:'الوسوم',
    confidentiality:'مستوى السرية', license:'الترخيص', copyright:'حقوق النشر',
    isPublicAccess:'متاح للعموم', isDownloadable:'قابل للتنزيل',
    retention:'جدول الاستبقاء', isLegalHold:'حجز قانوني', archivalReference:'المرجع الأرشيفي',
    digitizationStatus:'حالة الرقمنة', ocrQuality:'جودة OCR', ocrConfidence:'دقة OCR',
    resolution:'الدقة DPI', masterFileFormat:'تنسيق الملف', digitizedBy:'جهة الرقمنة',
    status:'حالة الأصل', qualityScore:'درجة الجودة', statusNote:'ملاحظات',
  }

  return (
    <div className="flex flex-col h-full bg-white border-r border-gray-100 overflow-hidden" style={{width:'380px'}}>
      {/* Header */}
      <div className={`p-4 ${typeInfo.light} border-b border-gray-100 flex-shrink-0`}>
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-3">
            <div className={`w-10 h-10 ${typeInfo.color} rounded-xl flex items-center justify-center text-xl text-white`}>
              {typeInfo.icon}
            </div>
            <div>
              <p className="font-black text-gray-900 text-sm leading-snug max-w-[220px]">{asset.titleAr}</p>
              <p className="text-[10px] font-mono text-gray-500 mt-0.5">{asset.assetCode}</p>
            </div>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-lg">✕</button>
        </div>

        <div className="flex items-center gap-2 mt-3">
          <span className={`inline-flex items-center gap-1 text-[11px] px-2.5 py-1 rounded-full font-medium ${statusCfg.cls}`}>
            <span className={`w-1.5 h-1.5 rounded-full ${statusCfg.dot}`}/>
            {asset.status}
          </span>
          <span className={`text-[11px] ${typeInfo.light} ${typeInfo.color.replace('bg-','text-').replace('-500','-700').replace('-600','-700')} px-2.5 py-1 rounded-full font-semibold`}>
            {typeInfo.nameAr}
          </span>
        </div>

        <div className="mt-3">
          <div className="flex justify-between text-[10px] text-gray-500 mb-1">
            <span>الرقمنة: {asset.digitizationStatus}</span>
            <span>{digitCfg.pct}%</span>
          </div>
          <div className="h-2 bg-white/60 rounded-full overflow-hidden">
            <div className={`h-full rounded-full ${digitCfg.cls}`} style={{width:`${digitCfg.pct}%`}}/>
          </div>
        </div>
      </div>

      {/* Aspect tabs */}
      <div className="flex overflow-x-auto border-b border-gray-100 flex-shrink-0 bg-gray-50">
        {ASPECTS.map(asp => (
          <button key={asp.code} onClick={() => setActiveAsp(asp.code)}
            className={`flex-shrink-0 flex items-center gap-1 px-3 py-2.5 text-[11px] font-medium border-b-2 transition-colors ${
              activeAsp===asp.code ? 'border-blue-600 text-blue-700 bg-white' : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}>
            {asp.icon} {asp.nameAr}
          </button>
        ))}
      </div>

      {/* Fields */}
      <div className="flex-1 overflow-y-auto p-4 space-y-2.5">
        {(fields[activeAsp] || []).map(f => {
          const val = asset[f]
          if (!val && val !== false) return null
          return (
            <div key={f} className="flex justify-between items-start gap-2 py-2 border-b border-gray-50">
              <span className="text-xs text-gray-400 flex-shrink-0 mt-0.5">{labels[f] || f}</span>
              <span className="text-xs font-medium text-gray-800 text-right max-w-[200px]">
                {typeof val === 'boolean' ? (val ? '✅ نعم' : '❌ لا') : val}
              </span>
            </div>
          )
        })}
        {(fields[activeAsp] || []).every(f => !asset[f] && asset[f] !== false) && (
          <div className="text-center py-8 text-gray-400">
            <p className="text-sm">لا توجد بيانات في هذا القسم</p>
          </div>
        )}
      </div>
    </div>
  )
}

// ─── Main Page ─────────────────────────────────────────────────────────────────
export default function ContentModelPage() {
  const { show, ToastContainer } = useToast()
  const [assets, setAssets]         = useState(MOCK_ASSETS)
  const [selected, setSelected]     = useState(null)
  const [showNew, setShowNew]       = useState(false)
  const [filterType, setFilterType] = useState('all')
  const [filterStatus, setFilterStatus] = useState('all')
  const [search, setSearch]         = useState('')
  const [view, setView]             = useState('grid')  // grid | list
  const [showSchema, setShowSchema] = useState(false)

  useEffect(() => {
    client.get('/api/v1/content-model/assets')
      .then(r => {
        const d = r.data?.data?.items || r.data?.data || r.data
        if (Array.isArray(d) && d.length > 0) setAssets(d)
      }).catch(() => {})
  }, [])

  const filtered = assets.filter(a =>
    (filterType === 'all' || a.type === filterType) &&
    (filterStatus === 'all' || a.status === filterStatus) &&
    (!search || a.titleAr?.includes(search) || a.assetCode?.includes(search) || a.primarySubject?.includes(search))
  )

  // Stats
  const stats = {
    total:    assets.length,
    approved: assets.filter(a => a.status === 'معتمد' || a.status === 'منشور').length,
    digitized:assets.filter(a => ['مُرقَّم','تم فحص الجودة','منشور رقمياً'].includes(a.digitizationStatus)).length,
    pending:  assets.filter(a => a.status === 'قيد المراجعة').length,
  }

  return (
    <div className="h-full flex flex-col">
      <ToastContainer />
      {showNew && (
        <NewAssetModal
          onClose={() => setShowNew(false)}
          onSuccess={asset => {
            setAssets(p => [asset, ...p])
            show(`✅ تم إنشاء: ${asset.titleAr}`, 'success')
            setShowNew(false)
            setSelected(asset)
          }}
        />
      )}

      {/* ── Header ── */}
      <div className="flex-shrink-0 space-y-4 mb-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-2xl font-black text-gray-900">نموذج المحتوى المؤسسي</h1>
            <p className="text-gray-400 text-sm mt-0.5">darah:knowledgeAsset — الإصدار 2.0</p>
          </div>
          <div className="flex gap-2">
            <button onClick={() => setShowSchema(p=>!p)}
              className={`border text-sm px-4 py-2 rounded-xl transition-colors ${showSchema?'bg-gray-800 text-white border-gray-800':'border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
              {showSchema ? '✕ إغلاق المخطط' : '📐 عرض المخطط'}
            </button>
            <button onClick={() => setShowNew(true)}
              className="bg-blue-700 text-white text-sm px-4 py-2 rounded-xl hover:bg-blue-800 transition-colors flex items-center gap-1.5 shadow-sm">
              + أصل معرفي جديد
            </button>
          </div>
        </div>

        {/* Schema overview */}
        {showSchema && (
          <div className="bg-gray-900 text-gray-100 rounded-2xl p-5 space-y-4">
            <p className="text-sm font-bold text-white flex items-center gap-2">📐 هيكل النموذج <span className="text-gray-400 font-normal text-xs">— 3 طبقات</span></p>
            <div className="grid grid-cols-3 gap-4 text-xs">
              <div className="bg-gray-800 rounded-xl p-3">
                <p className="text-blue-400 font-bold mb-2">Base Type</p>
                <p className="text-gray-300 font-mono">darah:knowledgeAsset</p>
                <p className="text-gray-500 mt-1">الأصل الأساسي لكل محتوى</p>
              </div>
              <div className="bg-gray-800 rounded-xl p-3">
                <p className="text-green-400 font-bold mb-2">Specialized Types ({ASSET_TYPES.length-1})</p>
                <div className="space-y-0.5">
                  {ASSET_TYPES.filter(t=>t.code!=='KnowledgeAsset').slice(0,5).map(t=>(
                    <p key={t.code} className="text-gray-300 font-mono text-[10px]">darah:{t.code.charAt(0).toLowerCase()+t.code.slice(1)}</p>
                  ))}
                  <p className="text-gray-500 text-[10px]">... +{ASSET_TYPES.length-6} more</p>
                </div>
              </div>
              <div className="bg-gray-800 rounded-xl p-3">
                <p className="text-yellow-400 font-bold mb-2">Aspects ({ASPECTS.length})</p>
                <div className="space-y-0.5">
                  {ASPECTS.map(a=>(
                    <p key={a.code} className="text-gray-300 font-mono text-[10px]">darah:{a.code}</p>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Stats */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {[
            { label:'إجمالي الأصول',   value:stats.total,    icon:'📦', color:'bg-indigo-50 text-indigo-700 border-indigo-100' },
            { label:'معتمدة ومنشورة',  value:stats.approved, icon:'✅', color:'bg-green-50 text-green-700 border-green-100' },
            { label:'مُرقَّمة رقمياً',  value:stats.digitized,icon:'💻', color:'bg-blue-50 text-blue-700 border-blue-100' },
            { label:'قيد المراجعة',    value:stats.pending,  icon:'🔄', color:'bg-yellow-50 text-yellow-700 border-yellow-100' },
          ].map(s => (
            <div key={s.label} className={`${s.color} border rounded-2xl p-4 flex items-center gap-3`}>
              <span className="text-2xl">{s.icon}</span>
              <div><p className="text-2xl font-black">{s.value}</p><p className="text-xs opacity-80">{s.label}</p></div>
            </div>
          ))}
        </div>
      </div>

      {/* ── Filters ── */}
      <div className="flex-shrink-0 bg-white rounded-2xl border border-gray-100 p-3 flex flex-wrap gap-2 mb-4">
        <div className="relative flex-1 min-w-40">
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300">🔍</span>
          <input value={search} onChange={e=>setSearch(e.target.value)} placeholder="البحث في الأصول..."
            className="w-full pr-9 pl-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
        </div>
        <select value={filterType} onChange={e=>setFilterType(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 py-2 text-sm text-gray-600 focus:outline-none">
          <option value="all">كل الأنواع</option>
          {ASSET_TYPES.filter(t=>t.code!=='KnowledgeAsset').map(t=>(
            <option key={t.code} value={t.code}>{t.icon} {t.nameAr}</option>
          ))}
        </select>
        <select value={filterStatus} onChange={e=>setFilterStatus(e.target.value)}
          className="border border-gray-200 rounded-xl px-3 py-2 text-sm text-gray-600 focus:outline-none">
          <option value="all">كل الحالات</option>
          {Object.keys(STATUS_CFG).map(s=><option key={s}>{s}</option>)}
        </select>
        <div className="flex border border-gray-200 rounded-xl overflow-hidden">
          <button onClick={()=>setView('grid')} className={`px-3 py-2 text-sm ${view==='grid'?'bg-gray-800 text-white':'text-gray-500 hover:bg-gray-50'}`}>⊞</button>
          <button onClick={()=>setView('list')} className={`px-3 py-2 text-sm ${view==='list'?'bg-gray-800 text-white':'text-gray-500 hover:bg-gray-50'}`}>☰</button>
        </div>
      </div>

      {/* ── Content ── */}
      <div className="flex-1 flex gap-4 overflow-hidden">
        {/* Assets */}
        <div className={`flex-1 overflow-y-auto ${selected ? 'hidden lg:block' : ''}`}>
          {filtered.length === 0 && (
            <div className="text-center py-16 text-gray-400">
              <div className="text-4xl mb-3">📭</div>
              <p>لا توجد أصول معرفية</p>
            </div>
          )}

          {view === 'grid' ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {filtered.map(a => (
                <AssetCard key={a.id} asset={a}
                  onClick={() => setSelected(selected?.id===a.id ? null : a)} />
              ))}
            </div>
          ) : (
            <div className="bg-white rounded-2xl border border-gray-100 overflow-hidden">
              <table className="w-full text-sm">
                <thead><tr className="bg-gray-50 border-b border-gray-100">
                  <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">الأصل</th>
                  <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">النوع</th>
                  <th className="px-4 py-3 text-right text-xs font-bold text-gray-400 hidden md:table-cell">الموضوع</th>
                  <th className="px-4 py-3 text-right text-xs font-bold text-gray-400">الحالة</th>
                  <th className="px-4 py-3 text-right text-xs font-bold text-gray-400 hidden lg:table-cell">الرقمنة</th>
                </tr></thead>
                <tbody className="divide-y divide-gray-50">
                  {filtered.map(a => {
                    const t = ASSET_TYPES.find(x=>x.code===a.type) || ASSET_TYPES[0]
                    const s = STATUS_CFG[a.status] || { cls:'bg-gray-100 text-gray-600', dot:'bg-gray-400' }
                    const d = DIGIT_CFG[a.digitizationStatus] || { pct:0, cls:'bg-gray-200' }
                    return (
                      <tr key={a.id} onClick={()=>setSelected(selected?.id===a.id?null:a)}
                        className={`cursor-pointer transition-colors ${selected?.id===a.id?'bg-blue-50':'hover:bg-gray-50'}`}>
                        <td className="px-4 py-3">
                          <p className="font-semibold text-gray-800 truncate max-w-[200px]">{a.titleAr}</p>
                          <p className="text-[10px] text-gray-400 font-mono">{a.assetCode}</p>
                        </td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center gap-1.5 text-[11px] px-2 py-0.5 rounded-full font-medium ${t.light} ${t.color.replace('bg-','text-').replace('-600','-700').replace('-500','-700')}`}>
                            {t.icon} {t.nameAr}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-xs text-gray-500 hidden md:table-cell truncate max-w-[150px]">{a.primarySubject||'—'}</td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center gap-1 text-[11px] px-2 py-0.5 rounded-full font-medium ${s.cls}`}>
                            <span className={`w-1.5 h-1.5 rounded-full ${s.dot}`}/>
                            {a.status}
                          </span>
                        </td>
                        <td className="px-4 py-3 hidden lg:table-cell">
                          <div className="flex items-center gap-2">
                            <div className="w-16 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                              <div className={`h-full rounded-full ${d.cls}`} style={{width:`${d.pct}%`}}/>
                            </div>
                            <span className="text-[10px] text-gray-400">{d.pct}%</span>
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Detail panel */}
        {selected && (
          <AssetDetailPanel asset={selected} onClose={() => setSelected(null)} />
        )}
      </div>
    </div>
  )
}
