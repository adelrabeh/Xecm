import { createContext, useContext, useState, useEffect } from 'react'
import React from 'react'

// ─── Full translation dictionary ──────────────────────────────────────────────
const T = {
  ar: {
    // System
    dir:'rtl', lang:'ar', fontFamily:"'Cairo', sans-serif",
    dateFormat:'ar-SA', numberFormat:'ar-SA-u-nu-arab',

    // Org
    org_name:'دارة الملك عبدالعزيز', org_sub:'نظام ECM',
    org_name_full:'King Abdulaziz Foundation for Research and Archives',

    // Nav
    nav_dashboard:'لوحة التحكم', nav_tasks:'المهام', nav_myfiles:'ملفاتي',
    nav_workflows:'سير العمل', nav_library:'المكتبة', nav_records:'السجلات',
    nav_content_model:'نموذج المحتوى', nav_search:'البحث', nav_admin:'الإدارة',

    // Common actions
    save:'حفظ', cancel:'إلغاء', delete:'حذف', edit:'تعديل', add:'إضافة',
    search:'بحث', filter:'تصفية', close:'إغلاق', confirm:'تأكيد',
    upload:'رفع', download:'تنزيل', preview:'معاينة', share:'مشاركة',
    print:'طباعة', export:'تصدير', import:'استيراد', refresh:'تحديث',
    yes:'نعم', no:'لا', all:'الكل', none:'لا شيء', other:'أخرى',
    loading:'جارٍ التحميل...', saving:'جارٍ الحفظ...', no_data:'لا توجد بيانات',
    back:'رجوع', next:'التالي', previous:'السابق', finish:'إنهاء',
    required_field:'هذا الحقل مطلوب', optional:'اختياري',
    total:'الإجمالي', count:'العدد', date:'التاريخ', time:'الوقت',

    // Auth
    login:'تسجيل الدخول', logout:'تسجيل الخروج',
    username:'اسم المستخدم', password:'كلمة المرور',
    change_password:'تغيير كلمة المرور',
    current_password:'كلمة المرور الحالية',
    new_password:'كلمة المرور الجديدة',
    confirm_password:'تأكيد كلمة المرور',
    password_strength:'قوة كلمة المرور',
    pw_very_weak:'ضعيفة جداً', pw_weak:'ضعيفة', pw_medium:'متوسطة',
    pw_strong:'قوية', pw_very_strong:'قوية جداً',
    welcome:'مرحباً بك',
    sign_in_subtitle:'نظام إدارة المحتوى المؤسسي',

    // Dashboard
    dashboard_title:'لوحة التحكم',
    total_docs:'إجمالي الوثائق', pending_tasks:'المهام المعلقة',
    recent_activity:'النشاط الأخير', quick_stats:'إحصائيات سريعة',
    my_tasks:'مهامي', my_documents:'وثائقي',
    overdue_tasks:'المهام المتأخرة', escalated_tasks:'المهام المُصعَّدة',
    completed_today:'مكتملة اليوم',

    // Documents
    docs_title:'ملفاتي', all_docs:'كل الوثائق',
    upload_doc:'رفع وثيقة', new_document:'وثيقة جديدة',
    doc_title_ar:'العنوان بالعربية', doc_title_en:'العنوان بالإنجليزية',
    doc_type:'نوع الوثيقة', file_size:'حجم الملف',
    version:'الإصدار', owner:'المالك', folder:'المجلد',
    checked_out:'مُستعار', checked_in:'متاح',
    classification:'التصنيف الأمني',
    cls_public:'عام', cls_internal:'داخلي', cls_confidential:'سري', cls_restricted:'سري للغاية',

    // Library
    library_title:'المكتبة', manage_folders:'إدارة المجلدات',
    new_folder:'مجلد جديد', folder_name:'اسم المجلد', parent_folder:'المجلد الأب',
    root_folder:'مجلد رئيسي', subfolder:'مجلد فرعي',
    admin_only_folders:'المجلدات تُدار من قِبَل الإدارة',
    upload_file:'رفع ملف', file_name:'اسم الملف',
    drag_drop:'اسحب وأفلت الملف هنا أو اضغط للاختيار',
    files_count:'ملف', folders_managed:'المجلدات يديرها الأدمن فقط',

    // Records
    records_title:'السجلات المؤسسية', new_record:'سجل جديد',
    record_number:'رقم السجل', domain:'النطاق', record_type:'نوع السجل',
    domain_legal:'قانوني وتعاقدي', domain_financial:'مالي',
    domain_admin:'إداري', domain_historical:'تاريخي وأرشيفي', domain_research:'بحثي وأكاديمي',
    submit_review:'إرسال للمراجعة',
    metadata:'البيانات الوصفية', attachments:'المرفقات', history:'السجل التاريخي',

    // Tasks
    tasks_title:'إدارة المهام', new_task:'مهمة جديدة',
    task_title:'عنوان المهمة', task_desc:'الوصف التفصيلي',
    assign_to:'تكليف إلى', due_date:'تاريخ الاستحقاق',
    priority:'الأولوية', status:'الحالة', department:'الإدارة / القسم',
    comments:'التعليقات', add_comment:'إضافة تعليق',
    escalate:'تصعيد', escalate_to:'التصعيد إلى',
    escalation_reason:'سبب التصعيد', escalation_level:'مستوى التصعيد',
    start_task:'بدء التنفيذ', send_review:'إرسال للمراجعة',
    approve:'اعتماد', reject:'رفض', close_task:'إغلاق المهمة',
    tags:'الوسوم',

    // Status labels
    st_new:'جديدة', st_assigned:'مُسنَدة', st_inprogress:'قيد التنفيذ',
    st_review:'قيد المراجعة', st_completed:'مكتملة', st_overdue:'متأخرة',
    st_cancelled:'ملغاة', st_draft:'مسودة', st_approved:'معتمد',
    st_archived:'مؤرشف', st_published:'منشور', st_rejected:'مرفوض',

    // Priority labels
    pr_low:'منخفضة', pr_medium:'متوسطة', pr_high:'عالية', pr_urgent:'عاجلة',

    // Workflows
    workflows_title:'سير العمل', new_workflow:'بناء سير عمل',
    workflow_title:'عنوان سير العمل', workflow_steps:'خطوات سير العمل',
    step_type:'نوع الخطوة', assignees:'المُكلَّفون', routing:'التوجيه',
    routing_seq:'تسلسلي', routing_parallel:'متوازي', routing_any:'أي واحد',
    action_review:'مراجعة', action_spelling:'مراجعة إملائية', action_language:'مراجعة لغوية',
    action_audit:'تدقيق', action_approve:'اعتماد', action_sign:'توقيع',
    action_print:'طباعة', action_archive:'أرشفة', action_notify:'إشعار',
    inbox:'صندوق المهام', my_workflows:'سير أعمالي',
    workflow_progress:'التقدم',

    // Content Model
    content_model_title:'نموذج المحتوى المؤسسي',
    asset_type:'نوع الأصل', asset_status:'حالة الأصل',
    digitization:'الرقمنة', ocr_quality:'جودة OCR',

    // Search
    search_title:'البحث المتقدم',
    search_placeholder:'ابحث في الوثائق والسجلات والأصول...',
    search_results:'نتائج البحث', no_results:'لا توجد نتائج',
    search_history:'آخر عمليات البحث', clear_history:'مسح',
    relevance:'الصلة', quick_search:'بحث سريع',

    // Users
    users_title:'إدارة المستخدمين', new_user:'مستخدم جديد',
    full_name:'الاسم الكامل', full_name_en:'الاسم بالإنجليزية',
    user_role:'الدور الوظيفي', active:'فعال', inactive:'معطل',
    grant_access:'منح وصول', revoke_access:'سحب الصلاحية',
    export_csv:'تصدير CSV',
    role_viewer:'مشاهد', role_employee:'موظف', role_supervisor:'مشرف',
    role_manager:'مدير القسم', role_admin:'مدير النظام',

    // Notifications
    notifications:'الإشعارات', mark_read:'تحديد كمقروء', no_notifs:'لا توجد إشعارات',
    notif_task:'مهمة جديدة', notif_overdue:'مهمة متأخرة',
    notif_shared:'مشاركة وثيقة', notif_comment:'تعليق جديد',
    notif_escalation:'تصعيد جديد',

    // Retention
    retention:'الاحتفاظ', retention_schedule:'جدول الاحتفاظ',
    legal_hold:'حجز قانوني', disposal:'الإتلاف',
    expires_at:'تاريخ الانتهاء', extend:'تمديد',
    approve_disposal:'اعتماد الإتلاف',

    // Errors
    err_required:'مطلوب', err_invalid_email:'بريد غير صحيح',
    err_file_size:'الملف أكبر من 50MB', err_unsupported:'نوع ملف غير مدعوم',
    err_session:'انتهت الجلسة', err_permission:'ليس لديك صلاحية',
  },

  en: {
    dir:'ltr', lang:'en', fontFamily:"'Inter', sans-serif",
    dateFormat:'en-US', numberFormat:'en-US',

    org_name:'Darah Foundation', org_sub:'ECM System',
    org_name_full:'King Abdulaziz Foundation for Research and Archives',

    nav_dashboard:'Dashboard', nav_tasks:'Tasks', nav_myfiles:'My Files',
    nav_workflows:'Workflows', nav_library:'Library', nav_records:'Records',
    nav_content_model:'Content Model', nav_search:'Search', nav_admin:'Admin',

    save:'Save', cancel:'Cancel', delete:'Delete', edit:'Edit', add:'Add',
    search:'Search', filter:'Filter', close:'Close', confirm:'Confirm',
    upload:'Upload', download:'Download', preview:'Preview', share:'Share',
    print:'Print', export:'Export', import:'Import', refresh:'Refresh',
    yes:'Yes', no:'No', all:'All', none:'None', other:'Other',
    loading:'Loading...', saving:'Saving...', no_data:'No data found',
    back:'Back', next:'Next', previous:'Previous', finish:'Finish',
    required_field:'This field is required', optional:'Optional',
    total:'Total', count:'Count', date:'Date', time:'Time',

    login:'Sign In', logout:'Sign Out',
    username:'Username', password:'Password',
    change_password:'Change Password',
    current_password:'Current Password', new_password:'New Password',
    confirm_password:'Confirm Password', password_strength:'Password Strength',
    pw_very_weak:'Very Weak', pw_weak:'Weak', pw_medium:'Medium',
    pw_strong:'Strong', pw_very_strong:'Very Strong',
    welcome:'Welcome', sign_in_subtitle:'Enterprise Content Management',

    dashboard_title:'Dashboard',
    total_docs:'Total Documents', pending_tasks:'Pending Tasks',
    recent_activity:'Recent Activity', quick_stats:'Quick Stats',
    my_tasks:'My Tasks', my_documents:'My Documents',
    overdue_tasks:'Overdue Tasks', escalated_tasks:'Escalated Tasks',
    completed_today:'Completed Today',

    docs_title:'My Files', all_docs:'All Documents',
    upload_doc:'Upload Document', new_document:'New Document',
    doc_title_ar:'Title (Arabic)', doc_title_en:'Title (English)',
    doc_type:'Document Type', file_size:'File Size',
    version:'Version', owner:'Owner', folder:'Folder',
    checked_out:'Checked Out', checked_in:'Available',
    classification:'Security Classification',
    cls_public:'Public', cls_internal:'Internal', cls_confidential:'Confidential', cls_restricted:'Strictly Confidential',

    library_title:'Library', manage_folders:'Manage Folders',
    new_folder:'New Folder', folder_name:'Folder Name', parent_folder:'Parent Folder',
    root_folder:'Root Folder', subfolder:'Subfolder',
    admin_only_folders:'Folders are managed by administrators',
    upload_file:'Upload File', file_name:'File Name',
    drag_drop:'Drag & drop file here or click to select',
    files_count:'file(s)', folders_managed:'Folders managed by admin only',

    records_title:'Institutional Records', new_record:'New Record',
    record_number:'Record No.', domain:'Domain', record_type:'Record Type',
    domain_legal:'Legal & Contractual', domain_financial:'Financial',
    domain_admin:'Administrative', domain_historical:'Historical & Archival', domain_research:'Research & Academic',
    submit_review:'Submit for Review',
    metadata:'Metadata', attachments:'Attachments', history:'History',

    tasks_title:'Task Management', new_task:'New Task',
    task_title:'Task Title', task_desc:'Detailed Description',
    assign_to:'Assign To', due_date:'Due Date',
    priority:'Priority', status:'Status', department:'Department',
    comments:'Comments', add_comment:'Add Comment',
    escalate:'Escalate', escalate_to:'Escalate To',
    escalation_reason:'Escalation Reason', escalation_level:'Escalation Level',
    start_task:'Start Task', send_review:'Send for Review',
    approve:'Approve', reject:'Reject', close_task:'Close Task',
    tags:'Tags',

    st_new:'New', st_assigned:'Assigned', st_inprogress:'In Progress',
    st_review:'Under Review', st_completed:'Completed', st_overdue:'Overdue',
    st_cancelled:'Cancelled', st_draft:'Draft', st_approved:'Approved',
    st_archived:'Archived', st_published:'Published', st_rejected:'Rejected',

    pr_low:'Low', pr_medium:'Medium', pr_high:'High', pr_urgent:'Urgent',

    workflows_title:'Workflows', new_workflow:'Build Workflow',
    workflow_title:'Workflow Title', workflow_steps:'Workflow Steps',
    step_type:'Step Type', assignees:'Assignees', routing:'Routing',
    routing_seq:'Sequential', routing_parallel:'Parallel', routing_any:'Any First',
    action_review:'Review', action_spelling:'Spell Check', action_language:'Language Review',
    action_audit:'Audit', action_approve:'Approve', action_sign:'Sign',
    action_print:'Print', action_archive:'Archive', action_notify:'Notify',
    inbox:'Inbox', my_workflows:'My Workflows', workflow_progress:'Progress',

    content_model_title:'Institutional Content Model',
    asset_type:'Asset Type', asset_status:'Asset Status',
    digitization:'Digitization', ocr_quality:'OCR Quality',

    search_title:'Advanced Search',
    search_placeholder:'Search documents, records, assets...',
    search_results:'Search Results', no_results:'No results found',
    search_history:'Recent Searches', clear_history:'Clear',
    relevance:'Relevance', quick_search:'Quick Search',

    users_title:'User Management', new_user:'New User',
    full_name:'Full Name', full_name_en:'Name (English)',
    user_role:'Role', active:'Active', inactive:'Inactive',
    grant_access:'Grant Access', revoke_access:'Revoke Access', export_csv:'Export CSV',
    role_viewer:'Viewer', role_employee:'Employee', role_supervisor:'Supervisor',
    role_manager:'Department Manager', role_admin:'System Admin',

    notifications:'Notifications', mark_read:'Mark All Read', no_notifs:'No notifications',
    notif_task:'New Task', notif_overdue:'Overdue Task',
    notif_shared:'Document Shared', notif_comment:'New Comment', notif_escalation:'New Escalation',

    retention:'Retention', retention_schedule:'Retention Schedule',
    legal_hold:'Legal Hold', disposal:'Disposal',
    expires_at:'Expires At', extend:'Extend', approve_disposal:'Approve Disposal',

    err_required:'Required', err_invalid_email:'Invalid email',
    err_file_size:'File exceeds 50MB limit', err_unsupported:'Unsupported file type',
    err_session:'Session expired', err_permission:'Permission denied',
  }
}

// ─── Context ─────────────────────────────────────────────────────────────────
const LangContext = createContext(null)

export function LangProvider({ children }) {
  const [lang, setLangState] = useState(
    () => localStorage.getItem('ecm_lang') || 'ar'
  )

  const applyLang = (l) => {
    document.documentElement.dir  = T[l].dir
    document.documentElement.lang = l
    document.documentElement.style.fontFamily = T[l].fontFamily
  }

  const setLang = (l) => {
    setLangState(l)
    localStorage.setItem('ecm_lang', l)
    applyLang(l)
    // Send preference to API (best effort)
    try {
      const token = localStorage.getItem('ecm_token')
      if (token) {
        fetch('/api/v1/users/language', {
          method: 'PUT',
          headers: { 'Authorization':`Bearer ${token}`, 'Content-Type':'application/json' },
          body: JSON.stringify({ language: l })
        }).catch(() => {})
      }
    } catch {}
  }

  useEffect(() => { applyLang(lang) }, [lang])

  const t = (key) => T[lang]?.[key] ?? T.ar[key] ?? key

  // Format dates according to selected language
  const fmtDate = (d) => {
    if (!d) return '—'
    try { return new Date(d).toLocaleDateString(T[lang].dateFormat) }
    catch { return d }
  }

  // Format numbers
  const fmtNum = (n) => {
    if (n == null) return '—'
    try { return new Intl.NumberFormat(T[lang].numberFormat).format(n) }
    catch { return n }
  }

  const isRTL = lang === 'ar'

  return React.createElement(LangContext.Provider,
    { value: { lang, setLang, t, isRTL, fmtDate, fmtNum } },
    children
  )
}

export function useLang() {
  const ctx = useContext(LangContext)
  if (!ctx) throw new Error('useLang must be used inside LangProvider')
  return ctx
}

// ─── HTTP header helper ───────────────────────────────────────────────────────
export function getLangHeader() {
  return { 'Accept-Language': localStorage.getItem('ecm_lang') || 'ar' }
}
