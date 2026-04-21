import React, { useState, useEffect, useRef } from 'react'
import { getFileBlob } from '../hooks/useFileStore'

const TYPE_META = {
  PDF:  { icon:'📕', color:'#dc2626', bg:'#fff1f0', label:'PDF' },
  DOCX: { icon:'📘', color:'#2563eb', bg:'#eff6ff', label:'Word' },
  DOC:  { icon:'📘', color:'#2563eb', bg:'#eff6ff', label:'Word' },
  XLSX: { icon:'📗', color:'#16a34a', bg:'#f0fdf4', label:'Excel' },
  XLS:  { icon:'📗', color:'#16a34a', bg:'#f0fdf4', label:'Excel' },
  PPTX: { icon:'📙', color:'#ea580c', bg:'#fff7ed', label:'PowerPoint' },
  ZIP:  { icon:'📦', color:'#9333ea', bg:'#faf5ff', label:'Archive' },
  PNG:  { icon:'🖼', color:'#db2777', bg:'#fdf2f8', label:'Image' },
  JPG:  { icon:'🖼', color:'#db2777', bg:'#fdf2f8', label:'Image' },
  JPEG: { icon:'🖼', color:'#db2777', bg:'#fdf2f8', label:'Image' },
}

// ─── Generate realistic Arabic content from file metadata ─────────────────────
function generateContent(file) {
  const title   = file?.titleAr || file?.name?.replace(/\.[^.]+$/,'') || 'الوثيقة'
  const summary = file?.summary || file?.description || ''
  const owner   = file?.owner || 'مدير النظام'
  const dept    = file?.department || file?.dept || 'الإدارة المعنية'
  const date    = file?.createdAt ? new Date(file.createdAt).toLocaleDateString('ar-SA',{year:'numeric',month:'long',day:'numeric'}) : new Date().toLocaleDateString('ar-SA',{year:'numeric',month:'long',day:'numeric'})
  const tags    = file?.tags || []
  const classification = file?.classification || 'داخلي'

  // Different body templates based on doc type
  const type = (file?.type || file?.fileType || '').toUpperCase()
  const docType = file?.type || ''

  let sections = []

  if (docType.includes('عقد') || docType.includes('اتفاق') || tags.includes('عقود')) {
    sections = [
      { h:'أولاً: أطراف العقد', body: `يُبرم هذا العقد بين دارة الملك عبدالعزيز، ممثلةً بـ ${owner}، بصفتها الطرف الأول، وبين الجهة المعنية المُشار إليها في ملحق هذا العقد، بصفتها الطرف الثاني.` },
      { h:'ثانياً: موضوع العقد', body: summary || `يتضمن هذا العقد الشروط والأحكام المنظِّمة للعلاقة التعاقدية بين الطرفين فيما يخص ${title}.` },
      { h:'ثالثاً: مدة العقد', body: 'تبدأ مدة تنفيذ هذا العقد من تاريخ توقيعه وتمتد لسنة كاملة قابلة للتجديد بموافقة الطرفين.' },
      { h:'رابعاً: الالتزامات', body: 'يلتزم الطرف الثاني بتنفيذ بنود هذا العقد وفق الشروط والمواصفات المحددة، ويلتزم الطرف الأول بالسداد في المواعيد المتفق عليها.' },
      { h:'خامساً: التوقيعات', body: 'يُعدّ هذا العقد نافذاً بعد التوقيع من قِبَل الطرفين وختمه بالأختام الرسمية.' },
    ]
  } else if (docType.includes('تقرير') || tags.includes('تقارير')) {
    sections = [
      { h:'ملخص تنفيذي', body: summary || `يُقدّم هذا التقرير نظرةً شاملة على ${title}، ويستعرض أبرز المؤشرات والنتائج خلال الفترة المرصودة.` },
      { h:'١. المقدمة', body: `في إطار منظومة التحول المؤسسي الذي تشهده دارة الملك عبدالعزيز، جاء هذا التقرير ليرصد واقع ${title} ويقدم توصيات قابلة للتطبيق.` },
      { h:'٢. المنهجية والبيانات', body: 'اعتمد الفريق في إعداد هذا التقرير على مصادر متعددة شملت البيانات الإدارية والمقابلات الميدانية والتقارير السابقة.' },
      { h:'٣. النتائج الرئيسية', body: 'أظهرت البيانات تحسناً ملموساً في معظم المؤشرات المستهدفة، مع وجود فجوات في بعض المجالات تستدعي اهتماماً أعمق.' },
      { h:'٤. التوصيات', body: 'توصي الدراسة بتعزيز الكوادر البشرية في الإدارات ذات الصلة، وتبني حلول رقمية متكاملة لرفع الكفاءة التشغيلية.' },
    ]
  } else if (docType.includes('سياسة') || docType.includes('لائحة') || tags.includes('سياسات')) {
    sections = [
      { h:'الهدف من السياسة', body: summary || `تهدف هذه السياسة إلى تنظيم العمل المتعلق بـ ${title} وضمان الامتثال للمعايير المعتمدة.` },
      { h:'النطاق والتطبيق', body: 'تسري أحكام هذه السياسة على جميع منسوبي الدارة دون استثناء، وتُلزم جميع الإدارات بالتقيد بها.' },
      { h:'المبادئ التوجيهية', body: 'تستند السياسة إلى مبادئ الشفافية والمساءلة والحوكمة الرشيدة، وتتوافق مع الأنظمة واللوائح المعمول بها في المملكة العربية السعودية.' },
      { h:'الإجراءات التنفيذية', body: 'تُطبَّق هذه السياسة عبر آليات محددة يُشرف عليها مدراء الإدارات بالتنسيق مع الإدارة العليا.' },
      { h:'المراجعة والتحديث', body: 'تُراجَع هذه السياسة سنوياً أو عند الحاجة، وتُعدَّل بما يتناسب مع المستجدات والتطورات التنظيمية.' },
    ]
  } else if (docType.includes('محضر') || tags.includes('محاضر')) {
    sections = [
      { h:'معلومات الاجتماع', body: `عُقد الاجتماع بتاريخ ${date} في مقر الدارة بحضور السادة أعضاء اللجنة المعنية وبرئاسة ${owner}.` },
      { h:'أولاً: افتتاح الاجتماع', body: 'افتتح الرئيس الاجتماع بالترحيب بالحاضرين، وأكد على أهمية الموضوعات المدرجة في جدول الأعمال.' },
      { h:'ثانياً: مناقشة جدول الأعمال', body: summary || `ناقش الحاضرون موضوع ${title} وتبادلوا الآراء والمقترحات بشأنه.` },
      { h:'ثالثاً: القرارات المتخذة', body: 'بعد المداولة، توصّل الأعضاء إلى جملة من القرارات والتوصيات التي ستُرفع للجهات المختصة للبت فيها.' },
      { h:'رابعاً: الختام', body: `انتهى الاجتماع في تمام الساعة، وتُودَع نسخة من هذا المحضر في ملف الاجتماعات الرسمية.` },
    ]
  } else if (docType.includes('خطاب') || docType.includes('مراسلة')) {
    sections = [
      { h:'', body: `الموضوع: ${title}` },
      { h:'', body: `السلام عليكم ورحمة الله وبركاته،\n\nتحية طيبة وبعد،` },
      { h:'', body: summary || `يسعدنا مراسلتكم بخصوص الموضوع المشار إليه أعلاه، آملين أن تجدوا في هذه المراسلة ما يوضح موقفنا ويُعبّر عن رغبتنا في التعاون المثمر.` },
      { h:'', body: `نأمل إفادتنا برأيكم في أقرب وقت ممكن، وإن كان لديكم أي استفسار فلا تترددوا في التواصل معنا على الأرقام والعناوين الرسمية للدارة.` },
      { h:'', body: `وتفضلوا بقبول خالص التحية والتقدير،\n\n${owner}\n${dept}` },
    ]
  } else {
    sections = [
      { h:'مقدمة', body: summary || `يُعدّ هذا المستند جزءاً من منظومة الوثائق الرسمية لدارة الملك عبدالعزيز، ويتناول موضوع ${title}.` },
      { h:'المحتوى الرئيسي', body: `في ضوء متطلبات ${dept} والأهداف الاستراتيجية للدارة، تم إعداد هذا المستند ليكون مرجعاً أساسياً في هذا الشأن.` },
      { h:'التفاصيل والإجراءات', body: 'تتضمن هذه الوثيقة كافة التفاصيل اللازمة لفهم الموضوع والتعامل معه وفق الإجراءات المعتمدة من الإدارة العليا.' },
      { h:'الخلاصة والتوصيات', body: 'استناداً إلى ما سبق، يُوصى باتخاذ الإجراءات اللازمة وفق الجدول الزمني المحدد وبالتنسيق مع جميع الأطراف المعنية.' },
    ]
  }

  return { title, summary, owner, dept, date, classification, tags, sections }
}

// ─── Document preview component ───────────────────────────────────────────────
function DocumentPreview({ file }) {
  const { title, owner, dept, date, classification, tags, sections } = generateContent(file)
  const type = (file?.fileType || file?.type || 'PDF').toUpperCase()
  const meta = TYPE_META[type] || TYPE_META.PDF
  const clsCfg = {
    'عام':        { bg:'#dcfce7', text:'#15803d', border:'#86efac' },
    'داخلي':      { bg:'#dbeafe', text:'#1d4ed8', border:'#93c5fd' },
    'سري':        { bg:'#ffedd5', text:'#c2410c', border:'#fdba74' },
    'سري للغاية': { bg:'#fee2e2', text:'#b91c1c', border:'#fca5a5' },
  }
  const cls = clsCfg[classification] || clsCfg['داخلي']

  return (
    <div className="min-h-full" style={{background:'#e5e7eb', padding:'24px 16px'}}>
      {/* Page */}
      <div style={{
        maxWidth:700, margin:'0 auto', background:'white',
        boxShadow:'0 4px 24px rgba(0,0,0,0.15)', borderRadius:4,
        minHeight:900, display:'flex', flexDirection:'column',
      }}>
        {/* Classification banner */}
        <div style={{
          background:cls.bg, borderBottom:`2px solid ${cls.border}`,
          padding:'6px 32px', textAlign:'center',
          fontSize:11, fontWeight:700, color:cls.text, letterSpacing:2,
        }}>
          ◈ {classification.toUpperCase()} — للاستخدام الرسمي فقط ◈
        </div>

        {/* Header */}
        <div style={{padding:'28px 40px 20px', borderBottom:'1px solid #e5e7eb'}}>
          {/* Logo row */}
          <div style={{display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:20}}>
            <div style={{textAlign:'right'}}>
              <div style={{fontSize:18, fontWeight:900, color:'#1e3a5f', fontFamily:'Arial'}}>
                دارة الملك عبدالعزيز
              </div>
              <div style={{fontSize:10, color:'#6b7280', marginTop:2}}>
                King Abdulaziz Foundation for Research and Archives
              </div>
            </div>
            <div style={{
              width:56, height:56, borderRadius:12, display:'flex',
              alignItems:'center', justifyContent:'center', fontSize:28,
              background: meta.bg, border:`2px solid ${meta.color}20`
            }}>
              {meta.icon}
            </div>
          </div>

          {/* Doc number + date row */}
          <div style={{
            display:'flex', justifyContent:'space-between', alignItems:'center',
            fontSize:11, color:'#6b7280', marginBottom:16,
            paddingBottom:12, borderBottom:'1px dashed #e5e7eb'
          }}>
            <div style={{textAlign:'right'}}>
              <span style={{color:'#9ca3af'}}>رقم الوثيقة: </span>
              <span style={{fontFamily:'monospace', color:'#374151', fontWeight:600}}>{file?.id || 'DOC-' + Date.now().toString().slice(-6)}</span>
            </div>
            <div style={{textAlign:'left'}}>
              <span style={{color:'#9ca3af'}}>التاريخ: </span>
              <span style={{fontWeight:600, color:'#374151'}}>{date}</span>
            </div>
          </div>

          {/* Title */}
          <h1 style={{
            fontSize:22, fontWeight:900, color:'#111827',
            textAlign:'right', lineHeight:1.4, margin:0,
            borderRight:`4px solid ${meta.color}`, paddingRight:12,
          }}>
            {title}
          </h1>

          {/* Meta row */}
          <div style={{
            display:'flex', gap:24, marginTop:12, fontSize:12, color:'#6b7280', textAlign:'right',
            flexWrap:'wrap'
          }}>
            {owner && <span>👤 {owner}</span>}
            {dept  && <span>🏛 {dept}</span>}
            {file?.version && <span>📋 الإصدار {file.version}</span>}
            {(file?.pages||file?.fileSize) && <span>📄 {file?.pages ? file.pages+' صفحة' : file.fileSize}</span>}
          </div>
        </div>

        {/* Body */}
        <div style={{padding:'28px 40px', flex:1, textAlign:'right', direction:'rtl'}}>
          {sections.map((s, i) => (
            <div key={i} style={{marginBottom:20}}>
              {s.h && (
                <h2 style={{
                  fontSize:14, fontWeight:800, color:'#1e3a5f',
                  margin:'0 0 8px 0', paddingBottom:4,
                  borderBottom:`1px solid ${meta.color}30`
                }}>{s.h}</h2>
              )}
              <p style={{
                fontSize:13, color:'#374151', lineHeight:1.9,
                margin:0, whiteSpace:'pre-line',
              }}>{s.body}</p>
            </div>
          ))}

          {/* Tags */}
          {tags?.length > 0 && (
            <div style={{marginTop:24, paddingTop:16, borderTop:'1px dashed #e5e7eb'}}>
              <div style={{fontSize:11, color:'#9ca3af', marginBottom:6}}>الكلمات المفتاحية:</div>
              <div style={{display:'flex', gap:6, flexWrap:'wrap'}}>
                {tags.map(t=>(
                  <span key={t} style={{
                    background:'#f3f4f6', border:'1px solid #e5e7eb',
                    borderRadius:20, padding:'2px 10px', fontSize:11, color:'#4b5563'
                  }}>#{t}</span>
                ))}
              </div>
            </div>
          )}

          {/* Signature area */}
          <div style={{
            marginTop:40, paddingTop:20, borderTop:'1px solid #e5e7eb',
            display:'grid', gridTemplateColumns:'1fr 1fr', gap:24,
          }}>
            {['المُعِد','المراجع'].map(label=>(
              <div key={label} style={{textAlign:'center'}}>
                <div style={{
                  height:48, borderBottom:'1px solid #d1d5db',
                  marginBottom:6,
                }}/>
                <div style={{fontSize:11, color:'#9ca3af'}}>{label}</div>
              </div>
            ))}
          </div>
        </div>

        {/* Footer */}
        <div style={{
          padding:'12px 40px', borderTop:'1px solid #e5e7eb',
          display:'flex', justifyContent:'space-between', alignItems:'center',
          background:'#f9fafb', fontSize:10, color:'#9ca3af',
        }}>
          <span>دارة الملك عبدالعزيز · نظام إدارة المحتوى ECM</span>
          <span style={{
            background:cls.bg, color:cls.text, border:`1px solid ${cls.border}`,
            borderRadius:4, padding:'1px 8px', fontWeight:600, fontSize:10,
          }}>{classification}</span>
          <span>{date}</span>
        </div>
      </div>
    </div>
  )
}

// ─── Excel preview ────────────────────────────────────────────────────────────
function ExcelPreview({ file }) {
  const title = file?.titleAr || file?.name?.replace(/\.[^.]+$/,'') || 'جدول البيانات'
  const rows = 15

  return (
    <div style={{height:'100%', background:'white', overflow:'auto'}}>
      {/* Excel toolbar mock */}
      <div style={{background:'#217346', color:'white', padding:'8px 16px', display:'flex', alignItems:'center', gap:12}}>
        <span style={{fontSize:20}}>📗</span>
        <span style={{fontWeight:700, fontSize:14}}>{title}</span>
        <div style={{marginRight:'auto', display:'flex', gap:8, fontSize:11}}>
          {['Sheet1','Sheet2','Sheet3'].map(s=>(
            <div key={s} style={{
              background: s==='Sheet1' ? 'white' : 'rgba(255,255,255,0.2)',
              color: s==='Sheet1' ? '#217346' : 'white',
              padding:'3px 12px', borderRadius:'4px 4px 0 0', cursor:'pointer', fontWeight:600,
            }}>{s}</div>
          ))}
        </div>
      </div>

      {/* Column letters */}
      <div style={{display:'flex', background:'#f3f4f6', borderBottom:'1px solid #d1d5db'}}>
        <div style={{width:40, padding:'4px 8px', fontSize:11, color:'#6b7280', borderLeft:'1px solid #d1d5db', flexShrink:0}}/>
        {['أ','ب','ج','د','هـ','و','ز','ح'].map(l=>(
          <div key={l} style={{flex:1, padding:'4px 8px', fontSize:11, fontWeight:600, color:'#374151', textAlign:'center', borderLeft:'1px solid #d1d5db', minWidth:90}}>
            {l}
          </div>
        ))}
      </div>

      {/* Rows */}
      {Array.from({length:rows}).map((_,i)=>(
        <div key={i} style={{display:'flex', borderBottom:'1px solid #f0f0f0'}}>
          <div style={{width:40, padding:'5px 8px', fontSize:11, color:'#9ca3af', textAlign:'center', borderLeft:'1px solid #d1d5db', background:'#f9fafb', flexShrink:0}}>
            {i+1}
          </div>
          {i===0
            ? ['الرقم','البيان','الكمية','السعر','الإجمالي','التاريخ','الحالة','الملاحظات'].map(h=>(
                <div key={h} style={{flex:1, padding:'5px 8px', fontSize:11, fontWeight:700, color:'white', background:'#217346', borderLeft:'1px solid #1a5c38', minWidth:90, textAlign:'right'}}>
                  {h}
                </div>
              ))
            : [
                i,
                `بند ${['الأول','الثاني','الثالث','الرابع','الخامس','السادس','السابع','الثامن','التاسع','العاشر','الحادي عشر','الثاني عشر','الثالث عشر','الرابع عشر'][i-1]}`,
                10+i*2,
                `${(100+i*15).toFixed(2)} ر.س`,
                `${((100+i*15)*(10+i*2)).toFixed(2)} ر.س`,
                `202${6-Math.floor(i/5)}-0${Math.floor(i/3)+1}-${String(i+1).padStart(2,'0')}`,
                i%3===0 ? '✅ مكتمل' : i%3===1 ? '🔄 جاري' : '⏳ معلق',
                i%4===0 ? 'يحتاج مراجعة' : '—',
              ].map((v,j)=>(
                <div key={j} style={{
                  flex:1, padding:'5px 8px', fontSize:11,
                  color: j===0 ? '#6b7280' : j===4 ? '#065f46' : j===6 ? (i%3===0?'#065f46':i%3===1?'#92400e':'#6b7280') : '#374151',
                  background: i%2===0 ? 'white' : '#f9fafb',
                  borderLeft:'1px solid #f0f0f0', minWidth:90, textAlign:'right',
                  fontWeight: j===4 ? 700 : 400,
                }}>
                  {v}
                </div>
              ))
          }
        </div>
      ))}

      {/* Sum row */}
      <div style={{display:'flex', background:'#f0fdf4', borderTop:'2px solid #217346'}}>
        <div style={{width:40, padding:'6px 8px', borderLeft:'1px solid #d1d5db', flexShrink:0}}/>
        {['', 'الإجمالي الكلي', '', '', `${Array.from({length:rows-1}).reduce((s,_,i)=>s+((100+(i+1)*15)*(10+(i+1)*2)),0).toFixed(2)} ر.س`, '', '', ''].map((v,j)=>(
          <div key={j} style={{flex:1, padding:'6px 8px', fontSize:12, fontWeight:700, color:'#065f46', borderLeft:'1px solid #d1d5db', minWidth:90, textAlign:'right'}}>
            {v}
          </div>
        ))}
      </div>
    </div>
  )
}

// ─── PowerPoint preview ───────────────────────────────────────────────────────
function PPTXPreview({ file, page, totalPages }) {
  const title = file?.titleAr || file?.name?.replace(/\.[^.]+$/,'') || 'العرض التقديمي'
  const slides = [
    { type:'title',   content: { h:title, sub: file?.summary||'دارة الملك عبدالعزيز' } },
    { type:'bullets', content: { h:'أبرز النقاط', items:['الهدف الرئيسي من هذا العرض','المنهجية المتبعة في التحليل','النتائج والتوصيات الرئيسية','خطة التنفيذ والجدول الزمني'] } },
    { type:'chart',   content: { h:'المؤشرات والإحصاءات', bars:[85,72,91,68,79,88] } },
    { type:'bullets', content: { h:'التوصيات', items:['تعزيز الموارد البشرية المتخصصة','تبني الحلول الرقمية المتكاملة','بناء الشراكات الاستراتيجية','متابعة التنفيذ ورفع التقارير الدورية'] } },
    { type:'end',     content: { h:'شكراً لكم', sub:'دارة الملك عبدالعزيز' } },
  ]
  const slide = slides[(page-1) % slides.length] || slides[0]

  const SlideContent = () => {
    if (slide.type === 'title') return (
      <div style={{display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center',height:'100%',textAlign:'center',gap:16}}>
        <div style={{fontSize:32,fontWeight:900,color:'white',lineHeight:1.3,maxWidth:'80%'}}>{slide.content.h}</div>
        <div style={{fontSize:15,color:'rgba(255,255,255,0.75)',maxWidth:'60%'}}>{slide.content.sub}</div>
        <div style={{fontSize:12,color:'rgba(255,255,255,0.5)',marginTop:20}}>{new Date().toLocaleDateString('ar-SA')}</div>
      </div>
    )
    if (slide.type === 'end') return (
      <div style={{display:'flex',flexDirection:'column',justifyContent:'center',alignItems:'center',height:'100%',textAlign:'center',gap:12}}>
        <div style={{fontSize:48}}>🎯</div>
        <div style={{fontSize:28,fontWeight:900,color:'white'}}>{slide.content.h}</div>
        <div style={{fontSize:14,color:'rgba(255,255,255,0.7)'}}>{slide.content.sub}</div>
      </div>
    )
    if (slide.type === 'bullets') return (
      <div style={{padding:'40px 60px',height:'100%',display:'flex',flexDirection:'column',justifyContent:'center'}}>
        <h2 style={{fontSize:22,fontWeight:800,color:'white',marginBottom:24,borderBottom:'2px solid rgba(255,255,255,0.3)',paddingBottom:12}}>{slide.content.h}</h2>
        <div style={{display:'flex',flexDirection:'column',gap:14}}>
          {slide.content.items.map((item,i)=>(
            <div key={i} style={{display:'flex',alignItems:'center',gap:12}}>
              <div style={{width:28,height:28,borderRadius:'50%',background:'rgba(255,255,255,0.2)',display:'flex',alignItems:'center',justifyContent:'center',fontSize:12,fontWeight:700,color:'white',flexShrink:0}}>{i+1}</div>
              <span style={{fontSize:15,color:'rgba(255,255,255,0.9)'}}>{item}</span>
            </div>
          ))}
        </div>
      </div>
    )
    if (slide.type === 'chart') return (
      <div style={{padding:'32px 48px',height:'100%',display:'flex',flexDirection:'column',justifyContent:'center'}}>
        <h2 style={{fontSize:20,fontWeight:800,color:'white',marginBottom:20}}>{slide.content.h}</h2>
        <div style={{display:'flex',alignItems:'flex-end',gap:12,height:140}}>
          {slide.content.bars.map((v,i)=>(
            <div key={i} style={{flex:1,display:'flex',flexDirection:'column',alignItems:'center',gap:4}}>
              <span style={{fontSize:11,color:'rgba(255,255,255,0.7)',fontWeight:700}}>{v}%</span>
              <div style={{width:'100%',borderRadius:'4px 4px 0 0',background:`rgba(255,255,255,${0.3+i*0.1})`,height:`${v}%`}}/>
              <span style={{fontSize:10,color:'rgba(255,255,255,0.6)'}}>Q{i+1}</span>
            </div>
          ))}
        </div>
      </div>
    )
    return null
  }

  return (
    <div style={{height:'100%',display:'flex',alignItems:'center',justifyContent:'center',background:'#1e293b',padding:32}}>
      <div style={{width:'100%',maxWidth:720,aspectRatio:'16/9',position:'relative',borderRadius:8,overflow:'hidden',boxShadow:'0 20px 60px rgba(0,0,0,0.5)',background:'linear-gradient(135deg,#1e40af,#1e3a8a)'}}>
        <SlideContent/>
        {/* Slide number */}
        <div style={{position:'absolute',bottom:12,left:16,fontSize:10,color:'rgba(255,255,255,0.4)'}}>
          {page} / {totalPages}
        </div>
        {/* Darah watermark */}
        <div style={{position:'absolute',bottom:12,right:16,fontSize:10,color:'rgba(255,255,255,0.3)',fontWeight:600}}>
          دارة الملك عبدالعزيز
        </div>
      </div>
    </div>
  )
}

// ─── Main PreviewModal ────────────────────────────────────────────────────────
export function PreviewModal({ file, onClose }) {
  const [page, setPage]   = useState(1)
  const [zoom, setZoom]   = useState(100)

  const type       = (file?.fileType || file?.type || 'PDF').toUpperCase()
  const meta       = TYPE_META[type] || TYPE_META.PDF
  const title      = file?.titleAr || file?.name || file?.title || 'معاينة'
  const totalPages = file?.pages || (type==='PPTX'?5 : type==='XLSX'?3 : type==='ZIP'?1 : 6)

  const blob   = file?.id ? getFileBlob(file.id) : null
  const blobUrl = blob?.url || file?.blobUrl || null
  const isPDF  = type === 'PDF'
  const isImg  = ['PNG','JPG','JPEG'].includes(type)

  useEffect(() => { setPage(1); setZoom(100) }, [file?.id])

  const handleDownload = () => {
    if (blobUrl) {
      const a = document.createElement('a')
      a.href = blobUrl
      a.download = file?.originalName || file?.name || title
      document.body.appendChild(a); a.click(); document.body.removeChild(a)
    } else {
      window.open(`/api/v1/documents/${file?.id}/download`, '_blank')
    }
  }

  const handlePrint = () => {
    if (blobUrl && isPDF) {
      const win = window.open(blobUrl, '_blank')
      if (win) win.onload = () => { win.focus(); win.print() }
      return
    }
    const pw = window.open('', '_blank')
    if (!pw) return
    const { title: t, owner, dept, date, classification, sections } = generateContent(file)
    pw.document.write(`<!DOCTYPE html><html dir="rtl"><head>
      <meta charset="utf-8"><title>${t}</title>
      <style>
        *{box-sizing:border-box} body{font-family:'Segoe UI',Arial,sans-serif;direction:rtl;margin:0;color:#111}
        .page{max-width:210mm;margin:0 auto;padding:20mm;min-height:297mm}
        .banner{background:#fee2e2;border-bottom:2px solid #fca5a5;padding:4px;text-align:center;font-size:10px;font-weight:700;color:#b91c1c;letter-spacing:2px}
        .header{border-bottom:1px solid #e5e7eb;padding-bottom:16px;margin-bottom:24px}
        .org{font-size:18px;font-weight:900;color:#1e3a5f} .sub{font-size:10px;color:#6b7280;margin-top:2px}
        h1{font-size:22px;font-weight:900;border-right:4px solid #2563eb;padding-right:10px;margin:16px 0 8px}
        .meta{font-size:12px;color:#6b7280;display:flex;gap:20px;flex-wrap:wrap}
        h2{font-size:14px;font-weight:800;color:#1e3a5f;margin:20px 0 6px;padding-bottom:4px;border-bottom:1px solid #e5e7eb}
        p{font-size:13px;line-height:1.9;color:#374151;margin:0 0 12px;white-space:pre-line}
        .footer{margin-top:40px;padding-top:12px;border-top:1px dashed #e5e7eb;display:flex;justify-content:space-between;font-size:10px;color:#9ca3af}
        .sig{display:grid;grid-template-columns:1fr 1fr;gap:24px;margin-top:40px}
        .sig-box{text-align:center} .sig-line{height:40px;border-bottom:1px solid #d1d5db;margin-bottom:6px} .sig-label{font-size:11px;color:#9ca3af}
        @page{size:A4;margin:15mm} @media print{body{margin:0}}
      </style></head><body>
      <div class="page">
        <div class="banner">◈ ${classification?.toUpperCase()} — للاستخدام الرسمي فقط ◈</div>
        <div class="header">
          <div class="org">دارة الملك عبدالعزيز</div>
          <div class="sub">King Abdulaziz Foundation for Research and Archives</div>
          <h1>${t}</h1>
          <div class="meta">
            ${owner?`<span>👤 ${owner}</span>`:''}
            ${dept?`<span>🏛 ${dept}</span>`:''}
            <span>📅 ${date}</span>
            ${file?.version?`<span>📋 الإصدار ${file.version}</span>`:''}
          </div>
        </div>
        ${sections.map(s=>`${s.h?`<h2>${s.h}</h2>`:''}<p>${s.body}</p>`).join('')}
        <div class="sig">
          <div class="sig-box"><div class="sig-line"/><div class="sig-label">المُعِد</div></div>
          <div class="sig-box"><div class="sig-line"/><div class="sig-label">المراجع</div></div>
        </div>
        <div class="footer">
          <span>دارة الملك عبدالعزيز · نظام ECM</span>
          <span>${classification}</span><span>${date}</span>
        </div>
      </div>
      </body></html>`)
    pw.document.close(); pw.focus(); setTimeout(()=>pw.print(), 600)
  }

  const showRealPreview = blobUrl && (isPDF || isImg)

  return (
    <div className="fixed inset-0 bg-black/80 z-[100] flex items-center justify-center p-3"
      onClick={e => e.target===e.currentTarget && onClose()}>
      <div className="bg-white rounded-2xl shadow-2xl flex flex-col"
        style={{width:'min(94vw,980px)', height:'92vh', overflow:'hidden'}}>

        {/* ── Toolbar ── */}
        <div style={{background:'#f8fafc', borderBottom:'1px solid #e2e8f0', padding:'10px 16px', display:'flex', alignItems:'center', gap:8, flexShrink:0}}>
          <div style={{width:36,height:36,borderRadius:10,display:'flex',alignItems:'center',justifyContent:'center',fontSize:20,flexShrink:0,background:meta.bg}}>
            {meta.icon}
          </div>
          <div style={{flex:1, minWidth:0}}>
            <p style={{fontWeight:700,color:'#111827',fontSize:14,margin:0,overflow:'hidden',textOverflow:'ellipsis',whiteSpace:'nowrap'}}>{title}</p>
            <p style={{fontSize:10,color:'#9ca3af',margin:0}}>
              {meta.label} · {file?.fileSize||file?.size||''} · v{file?.version||'1.0'}
              {file?.classification && ` · ${file.classification}`}
              {blobUrl && <span style={{color:'#16a34a',fontWeight:600}}> · ✓ ملف حقيقي</span>}
            </p>
          </div>

          {/* Zoom (non-real) */}
          {!showRealPreview && ['XLSX','XLS','PPTX'].indexOf(type)===-1 && (
            <div style={{display:'flex',alignItems:'center',border:'1px solid #e2e8f0',borderRadius:10,overflow:'hidden'}}>
              <button onClick={()=>setZoom(z=>Math.max(70,z-10))}
                style={{padding:'6px 10px',background:'none',border:'none',cursor:'pointer',fontSize:14,color:'#6b7280'}}>−</button>
              <span style={{padding:'0 8px',fontSize:12,color:'#374151',minWidth:42,textAlign:'center'}}>{zoom}%</span>
              <button onClick={()=>setZoom(z=>Math.min(150,z+10))}
                style={{padding:'6px 10px',background:'none',border:'none',cursor:'pointer',fontSize:14,color:'#6b7280'}}>+</button>
            </div>
          )}

          <button onClick={handleDownload}
            style={{display:'flex',alignItems:'center',gap:6,background:'#1d4ed8',color:'white',border:'none',borderRadius:10,padding:'7px 14px',fontSize:12,fontWeight:700,cursor:'pointer',whiteSpace:'nowrap'}}>
            ⬇️ تنزيل
          </button>
          <button onClick={handlePrint}
            style={{display:'flex',alignItems:'center',gap:6,background:'none',border:'1px solid #e2e8f0',borderRadius:10,padding:'7px 14px',fontSize:12,color:'#374151',cursor:'pointer',whiteSpace:'nowrap'}}>
            🖨️ طباعة
          </button>
          <button onClick={onClose}
            style={{width:32,height:32,borderRadius:8,background:'none',border:'1px solid #e2e8f0',cursor:'pointer',fontSize:18,color:'#6b7280',display:'flex',alignItems:'center',justifyContent:'center'}}>
            ✕
          </button>
        </div>

        {/* ── Content ── */}
        <div style={{flex:1, overflow:'auto', position:'relative'}}>
          {showRealPreview && isPDF  && <iframe src={blobUrl} style={{width:'100%',height:'100%',border:'none'}}/>}
          {showRealPreview && isImg  && (
            <div style={{height:'100%',display:'flex',alignItems:'center',justifyContent:'center',background:'#111'}}>
              <img src={blobUrl} alt={title} style={{maxHeight:'100%',maxWidth:'100%',objectFit:'contain',borderRadius:8}}/>
            </div>
          )}
          {!showRealPreview && type==='XLSX' && <ExcelPreview file={file}/>}
          {!showRealPreview && type==='XLS'  && <ExcelPreview file={file}/>}
          {!showRealPreview && type==='PPTX' && <PPTXPreview file={file} page={page} totalPages={totalPages}/>}
          {!showRealPreview && !['XLSX','XLS','PPTX'].includes(type) && (
            <div style={{transform:`scale(${zoom/100})`, transformOrigin:'top center', minHeight: zoom<100?`${10000/zoom}%`:'100%'}}>
              <DocumentPreview file={file}/>
            </div>
          )}
        </div>

        {/* ── Footer ── */}
        <div style={{background:'#f8fafc',borderTop:'1px solid #e2e8f0',padding:'8px 16px',display:'flex',alignItems:'center',justifyContent:'space-between',flexShrink:0}}>
          <div style={{display:'flex',alignItems:'center',gap:8}}>
            <button onClick={()=>setPage(p=>Math.max(1,p-1))} disabled={page===1}
              style={{width:28,height:28,borderRadius:8,border:'1px solid #e2e8f0',background:'white',cursor:page===1?'not-allowed':'pointer',opacity:page===1?0.4:1,fontSize:12}}>←</button>
            <span style={{fontSize:12,color:'#6b7280'}}>صفحة <strong style={{color:'#111'}}>{page}</strong> / <strong style={{color:'#111'}}>{totalPages}</strong></span>
            <button onClick={()=>setPage(p=>Math.min(totalPages,p+1))} disabled={page===totalPages}
              style={{width:28,height:28,borderRadius:8,border:'1px solid #e2e8f0',background:'white',cursor:page===totalPages?'not-allowed':'pointer',opacity:page===totalPages?0.4:1,fontSize:12}}>→</button>
          </div>
          <div style={{display:'flex',gap:6}}>
            {(file?.tags||[]).slice(0,4).map(t=>(
              <span key={t} style={{background:'white',border:'1px solid #e2e8f0',borderRadius:20,padding:'2px 8px',fontSize:10,color:'#6b7280'}}>#{t}</span>
            ))}
          </div>
          <span style={{fontSize:10,color:'#9ca3af'}}>
            {file?.owner && `${file.owner} · `}
            {file?.createdAt ? new Date(file.createdAt).toLocaleDateString('ar-SA') : ''}
          </span>
        </div>
      </div>
    </div>
  )
}
