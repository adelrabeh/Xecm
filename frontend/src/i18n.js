import { createContext, useContext, useState, useEffect } from 'react'
import React from 'react'

// ─── Complete translation table — every Arabic string in the app ──────────────
const T = {
  ar: {
    dir: 'rtl', lang: 'ar', font: "'Cairo', sans-serif",
    dateLocale: 'ar-SA', numLocale: 'ar-SA',

    // ── Org ──
    org: 'دارة الملك عبدالعزيز', org_sub: 'نظام ECM',

    // ── Auth ──
    login: 'تسجيل الدخول', logout: 'تسجيل الخروج',
    username: 'اسم المستخدم', password: 'كلمة المرور',
    change_password: 'تغيير كلمة المرور',
    current_password: 'كلمة المرور الحالية',
    new_password: 'كلمة المرور الجديدة',
    confirm_password: 'تأكيد كلمة المرور',
    pw_strength: 'القوة',
    pw_very_weak: 'ضعيفة جداً', pw_weak: 'ضعيفة', pw_medium: 'متوسطة',
    pw_strong: 'قوية', pw_very_strong: 'قوية جداً',
    sign_in_title: 'تسجيل الدخول',
    sign_in_sub: 'نظام إدارة المحتوى المؤسسي',

    // ── Nav ──
    nav_dashboard: 'لوحة التحكم', nav_tasks: 'المهام',
    nav_myfiles: 'ملفاتي', nav_workflows: 'سير العمل',
    nav_library: 'المكتبة', nav_records: 'السجلات',
    nav_content_model: 'نموذج المحتوى', nav_search: 'البحث', nav_admin: 'الإدارة',

    // ── Common actions ──
    save: 'حفظ', cancel: 'إلغاء', delete: 'حذف', edit: 'تعديل',
    add: 'إضافة', upload: 'رفع', download: 'تنزيل', preview: 'معاينة',
    share: 'مشاركة', print: 'طباعة', close: 'إغلاق', confirm: 'تأكيد',
    back: 'رجوع', next: 'التالي', prev: 'السابق', finish: 'إنهاء',
    search: 'بحث', filter: 'تصفية', refresh: 'تحديث', export: 'تصدير',
    all: 'الكل', yes: 'نعم', no: 'لا', none: 'لا شيء',
    loading: 'جارٍ التحميل...', saving: 'جارٍ الحفظ...',
    no_data: 'لا توجد بيانات', required: 'مطلوب', optional: 'اختياري',
    total: 'الإجمالي', details: 'التفاصيل',

    // ── Common fields ──
    name: 'الاسم', name_en: 'الاسم بالإنجليزية',
    title_ar: 'العنوان بالعربية', title_en: 'العنوان بالإنجليزية',
    description: 'الوصف', summary: 'الملخص', notes: 'ملاحظات',
    date: 'التاريخ', created_at: 'تاريخ الإنشاء', updated_at: 'آخر تعديل',
    due_date: 'تاريخ الاستحقاق', doc_date: 'تاريخ الوثيقة',
    status: 'الحالة', priority: 'الأولوية', type: 'النوع',
    department: 'الإدارة / القسم', owner: 'المالك', version: 'الإصدار',
    tags: 'الكلمات المفتاحية', classification: 'مستوى السرية',
    attachments: 'المرفقات', comments: 'التعليقات',
    email: 'البريد الإلكتروني', role: 'الدور',

    // ── Status labels ──
    st_draft: 'مسودة', st_review: 'قيد المراجعة', st_approved: 'معتمد',
    st_archived: 'مؤرشف', st_rejected: 'مرفوض', st_cancelled: 'ملغى',
    st_published: 'منشور', st_new: 'جديدة', st_assigned: 'مُسنَدة',
    st_inprogress: 'قيد التنفيذ', st_completed: 'مكتملة', st_overdue: 'متأخرة',
    st_active: 'نشط', st_inactive: 'معطل',

    // ── Priority labels ──
    pr_low: 'منخفضة', pr_medium: 'متوسطة', pr_high: 'عالية', pr_urgent: 'عاجلة',

    // ── Security / classification ──
    cls_public: 'عام', cls_internal: 'داخلي', cls_confidential: 'سري',
    cls_restricted: 'سري للغاية',

    // ── Dashboard ──
    dashboard_title: 'لوحة التحكم',
    welcome: 'مرحباً', welcome_back: 'مرحباً بعودتك',
    total_docs: 'إجمالي الوثائق', total_records: 'إجمالي السجلات',
    pending_tasks: 'المهام المعلقة', completed_tasks: 'المهام المكتملة',
    overdue_tasks: 'المهام المتأخرة', escalated_tasks: 'المهام المُصعَّدة',
    my_tasks: 'مهامي', recent_docs: 'وثائق حديثة',
    by_status: 'حسب الحالة', by_dept: 'حسب القسم', by_level: 'حسب المستوى',
    escalation_stats: 'إحصائيات التصعيد',
    assigned_to_me: 'المُكلَّفة لي', pending_escalations: 'قيد الانتظار',
    overdue_escalated: 'متأخرة مُصعَّدة', resolved_escalations: 'محلولة',

    // ── Documents ──
    docs_title: 'ملفاتي', all_docs: 'كل الوثائق',
    upload_doc: 'رفع وثيقة', doc_type: 'نوع الوثيقة',
    file_size: 'حجم الملف', checked_out: 'مُستعار', checked_in: 'متاح',
    checkout: 'استعارة', checkin: 'إعادة',
    admin_view: 'عرض المدير', no_docs: 'لا توجد وثائق',
    send_approval: 'إرسال للاعتماد', archive_doc: 'أرشفة',
    favorites: 'المفضلة', shared_with_me: 'مُشاركة معي',
    file_count: 'وثيقة',

    // ── Library ──
    library_title: 'المكتبة', manage_folders: 'إدارة المجلدات',
    all_files: 'كل الملفات', new_folder: 'مجلد جديد',
    folder_name: 'اسم المجلد', parent_folder: 'المجلد الأب',
    root_folder: 'مجلد رئيسي', subfolder: 'مجلد فرعي',
    upload_file: 'رفع ملف', file_name: 'اسم الملف',
    drag_drop: 'اسحب وأفلت الملف هنا أو اضغط للاختيار',
    supported_types: 'PDF, DOCX, XLSX, PPTX, ZIP — حتى 50MB',
    admin_folders_note: '🔐 المجلدات تُدار من قِبَل الإدارة',
    create_folder_btn: '+ إنشاء مجلد جديد',
    choose_folder: '— اختر المجلد —',
    empty_folder: 'لا توجد ملفات لك في هذا المجلد',
    no_library_files: 'لا توجد ملفات خاصة بك',
    upload_first: '📤 رفع أول ملف',
    save_changes: 'حفظ التغييرات',
    add_folder_btn: '+ إضافة مجلد',
    folder_management_title: 'إدارة هيكل المجلدات',
    admin_permission_note: '🔐 صلاحية مدير النظام فقط',
    subfolder_count: 'ملف فرعي',
    step_file: 'الملف', step_folder: 'المجلد', step_details: 'التفاصيل',
    select_folder_label: 'اختر المجلد',
    select_folder_note: 'حدد المجلد الذي تريد حفظ الملف فيه من هيكل المكتبة',
    no_folders_msg: 'لا توجد مجلدات في المكتبة',
    no_folders_sub: 'يجب على المدير إنشاء مجلدات أولاً',
    selected_path: 'سيُحفظ في:',
    file_name_label: 'اسم الملف',
    file_name_no_ext: 'اسم الملف بدون الامتداد',

    // ── Records ──
    records_title: 'السجلات المؤسسية', new_record: 'إنشاء سجل جديد',
    record_number: 'رقم السجل', domain: 'النطاق', record_type: 'نوع السجل',
    domain_legal: 'قانوني وتعاقدي', domain_fin: 'مالي',
    domain_admin: 'إداري', domain_hist: 'تاريخي وأرشيفي', domain_res: 'بحثي وأكاديمي',
    step_domain: 'النطاق', step_type: 'النوع',
    step_core: 'البيانات الأساسية', step_domain_fields: 'بيانات النطاق',
    step_files: 'المرفقات',
    choose_domain: 'اختر النطاق', choose_type: 'نوع السجل في',
    no_extra_fields: 'لا توجد حقول إضافية لهذا النوع',
    add_attachments: 'أضف المرفقات',
    drop_files: 'PDF, DOCX, XLSX, صور — حتى 50MB لكل ملف',
    file_ready: 'ملف جاهز للرفع',
    add_later: 'يمكن إضافة المرفقات لاحقاً',
    save_record: 'حفظ السجل',
    record_created: 'تم إنشاء السجل بنجاح',
    security_level: 'مستوى السرية',
    filter_domain: 'كل النطاقات', filter_status: 'كل الحالات',
    submit_for_review: 'إرسال للمراجعة',
    approve_record: 'اعتماد', reject_record: 'رفض', archive_record: 'أرشفة',
    domain_specific: 'بيانات',
    keywords: 'الكلمات المفتاحية',
    no_records: 'لا توجد سجلات',
    create_first_record: '+ أنشئ أول سجل',
    records_system: 'نظام السجلات المبني على البيانات الوصفية',
    kpi_total: 'إجمالي السجلات', kpi_draft: 'مسودة',
    kpi_review: 'قيد المراجعة', kpi_approved: 'معتمد',

    // ── Tasks ──
    tasks_title: 'إدارة المهام', new_task: 'مهمة جديدة',
    task_title_field: 'عنوان المهمة', task_desc: 'الوصف التفصيلي',
    assign_to: 'تكليف إلى', choose_dept: '— اختر القسم —',
    choose_employee: '— اختر الموظف —', choose_priority: 'الأولوية',
    task_tags: 'الوسوم (مفصولة بفاصلة)',
    tags_placeholder: 'تدريب، مالي، عاجل، ...',
    title_placeholder: 'عنوان واضح وموجز...',
    desc_placeholder: 'وصف تفصيلي للمهمة والنتائج المطلوبة...',
    date_placeholder: 'يوم/شهر/سنة',
    save_edits: 'حفظ التعديلات',
    create_task: 'إنشاء المهمة',
    edit_task: 'تعديل المهمة',
    status_actions: 'الإجراءات',
    status_history: 'سجل الحالات',
    start_task: 'بدء التنفيذ', send_review: 'إرسال للمراجعة',
    close_approve: 'إغلاق ✅', cancel_task: 'إلغاء المهمة',
    escalate: 'تصعيد', escalate_to_mgr: 'تصعيد للمدير',
    add_comment: 'إضافة تعليق', add_attachment: 'إضافة',
    comment_placeholder: 'اكتب تعليقك...',
    no_attachments: 'لا توجد مرفقات',
    no_comments: 'لا توجد تعليقات',
    task_created_by: 'أنشأها', task_assigned_to: 'مُكلَّف',
    task_created_at: 'أُنشئت بتاريخ',
    board_view: 'لوحة', list_view: 'قائمة',
    reports: 'التقارير', tasks_reports_title: 'تقارير المهام',
    filter_all_status: 'كل الحالات', filter_all_prio: 'كل الأولويات',
    filter_all_dept: 'كل الأقسام', search_tasks: 'بحث...',
    no_tasks: 'لا توجد مهام',
    task_kpi_total: 'إجمالي المهام', task_kpi_inprog: 'قيد التنفيذ',
    task_kpi_overdue: 'متأخرة', task_kpi_done: 'مكتملة',
    task_kpi_escalated: 'مُصعَّدة',

    // ── Escalation ──
    escalate_task: 'تصعيد المهمة',
    your_role: 'دورك',
    escalated_task_label: 'المهمة المُصعَّدة:',
    escalate_to_label: 'التصعيد إلى',
    choose_person: '— اختر الشخص المسؤول —',
    escalation_reason_label: 'سبب التصعيد',
    escalation_reason_ph: 'اشرح بوضوح لماذا تحتاج لتصعيد هذه المهمة...',
    escalation_rules: 'قواعد التصعيد:',
    esc_rule1: 'التصعيد لا يُغير حالة المهمة تلقائياً',
    esc_rule2: 'سيُسجَّل في سجل التدقيق',
    esc_rule3: 'الطرف المُصعَّد إليه سيتلقى إشعاراً فورياً',
    confirm_escalation: 'تأكيد التصعيد',
    escalating: 'جارٍ التصعيد...',
    escalation_history: 'سجل التصعيد',
    esc_level: 'المستوى',
    esc_pending: 'معلق', esc_accepted: 'مقبول', esc_resolved: 'محلول',
    esc_reason_label: 'السبب:',
    level1: 'المستوى الأول: إلى المشرف',
    level2: 'المستوى الثاني: إلى مدير القسم',
    level3: 'المستوى الثالث: تصعيد متقاطع',
    no_valid_targets: 'لا يوجد مستهدف مناسب لمستوى تصعيدك',

    // ── Search ──
    search_title: 'البحث المتقدم',
    search_ph: 'ابحث في الوثائق والسجلات والأصول المعرفية...',
    search_btn: 'بحث',
    search_scope: 'ابحث في كامل محتوى الدارة',
    no_results: 'لا توجد نتائج',
    no_results_for: 'لا توجد نتائج لـ',
    try_other: 'جرّب كلمات أخرى أو تحقق من الإملاء',
    results_for: 'نتيجة لـ',
    sort_relevance: 'الصلة ↓',
    sort_by: 'الترتيب حسب:',
    search_history: 'آخر عمليات البحث',
    clear_history: 'مسح',
    quick_search: 'بحث سريع',
    search_empty_title: 'ابحث في كامل محتوى الدارة',

    // ── Users / Admin ──
    users_title: 'إدارة المستخدمين', new_user: 'مستخدم جديد',
    full_name: 'الاسم الكامل', user_email: 'البريد الإلكتروني',
    user_dept: 'الإدارة / القسم', user_role: 'الدور',
    user_status: 'الحالة', user_actions: 'الإجراءات',
    search_users: 'البحث في المستخدمين...',
    choose_dept: '— اختر القسم —', choose_role: 'المستخدم',
    export_csv: 'تصدير CSV',
    edit_user_title: 'تعديل مستخدم',
    add_user_title: 'إضافة مستخدم جديد',
    grant_access: '🔐 منح وصول', full_access: '🔓 وصول كامل',
    enable_user: 'تفعيل', disable_user: 'تعطيل',
    delete_user: 'حذف',
    user_inactive: 'معطّل',
    filter_all: 'الكل', filter_active: 'الفعالون', filter_inactive: 'المعطلون',
    role_viewer: 'مشاهد', role_employee: 'موظف', role_supervisor: 'مشرف',
    role_manager: 'مدير القسم', role_admin: 'مدير النظام',

    // ── Workflows ──
    workflows_title: 'سير العمل', new_workflow: 'بناء سير عمل',
    inbox_title: 'صندوق المهام', my_workflows_title: 'سير أعمالي',
    workflow_name: 'اسم سير العمل', workflow_steps: 'الخطوات',
    send_for_approval: '📤 إرسال للاعتماد',
    no_inbox: 'لا توجد مهام في صندوقك',
    no_workflows: 'لا توجد مسارات بعد',

    // ── Notifications ──
    notifications: 'الإشعارات', mark_read: 'تحديد الكل كمقروء',
    no_notifs: 'لا توجد إشعارات',

    // ── Content Model ──
    content_model_title: 'نموذج المحتوى المؤسسي',

    // ── Password modal ──
    pw_modal_title: 'تغيير كلمة المرور',
    pw_modal_sub: 'يُنصح بتغييرها دورياً للحفاظ على أمان حسابك',
    pw_current_label: 'كلمة المرور الحالية',
    pw_new_label: 'كلمة المرور الجديدة',
    pw_confirm_label: 'تأكيد كلمة المرور',
    pw_min_hint: '8 أحرف على الأقل، حروف كبيرة وصغيرة، أرقام، ورموز',
    pw_strength_label: 'القوة:',
    pw_change_btn: '🔑 تغيير كلمة المرور',
    pw_saving: '⏳ جارٍ الحفظ...',
  },

  en: {
    dir: 'ltr', lang: 'en', font: "'Inter', sans-serif",
    dateLocale: 'en-US', numLocale: 'en-US',

    org: 'Darah Foundation', org_sub: 'ECM System',

    login: 'Sign In', logout: 'Sign Out',
    username: 'Username', password: 'Password',
    change_password: 'Change Password',
    current_password: 'Current Password', new_password: 'New Password',
    confirm_password: 'Confirm Password', pw_strength: 'Strength',
    pw_very_weak: 'Very Weak', pw_weak: 'Weak', pw_medium: 'Medium',
    pw_strong: 'Strong', pw_very_strong: 'Very Strong',
    sign_in_title: 'Sign In', sign_in_sub: 'Enterprise Content Management',

    nav_dashboard: 'Dashboard', nav_tasks: 'Tasks', nav_myfiles: 'My Files',
    nav_workflows: 'Workflows', nav_library: 'Library', nav_records: 'Records',
    nav_content_model: 'Content Model', nav_search: 'Search', nav_admin: 'Admin',

    save: 'Save', cancel: 'Cancel', delete: 'Delete', edit: 'Edit',
    add: 'Add', upload: 'Upload', download: 'Download', preview: 'Preview',
    share: 'Share', print: 'Print', close: 'Close', confirm: 'Confirm',
    back: 'Back', next: 'Next', prev: 'Previous', finish: 'Finish',
    search: 'Search', filter: 'Filter', refresh: 'Refresh', export: 'Export',
    all: 'All', yes: 'Yes', no: 'No', none: 'None',
    loading: 'Loading...', saving: 'Saving...',
    no_data: 'No data found', required: 'Required', optional: 'Optional',
    total: 'Total', details: 'Details',

    name: 'Name', name_en: 'Name (English)',
    title_ar: 'Title (Arabic)', title_en: 'Title (English)',
    description: 'Description', summary: 'Summary', notes: 'Notes',
    date: 'Date', created_at: 'Created', updated_at: 'Updated',
    due_date: 'Due Date', doc_date: 'Document Date',
    status: 'Status', priority: 'Priority', type: 'Type',
    department: 'Department', owner: 'Owner', version: 'Version',
    tags: 'Keywords', classification: 'Security Classification',
    attachments: 'Attachments', comments: 'Comments',
    email: 'Email', role: 'Role',

    st_draft: 'Draft', st_review: 'Under Review', st_approved: 'Approved',
    st_archived: 'Archived', st_rejected: 'Rejected', st_cancelled: 'Cancelled',
    st_published: 'Published', st_new: 'New', st_assigned: 'Assigned',
    st_inprogress: 'In Progress', st_completed: 'Completed', st_overdue: 'Overdue',
    st_active: 'Active', st_inactive: 'Inactive',

    pr_low: 'Low', pr_medium: 'Medium', pr_high: 'High', pr_urgent: 'Urgent',

    cls_public: 'Public', cls_internal: 'Internal',
    cls_confidential: 'Confidential', cls_restricted: 'Strictly Confidential',

    dashboard_title: 'Dashboard',
    welcome: 'Welcome', welcome_back: 'Welcome back',
    total_docs: 'Total Documents', total_records: 'Total Records',
    pending_tasks: 'Pending Tasks', completed_tasks: 'Completed Tasks',
    overdue_tasks: 'Overdue Tasks', escalated_tasks: 'Escalated Tasks',
    my_tasks: 'My Tasks', recent_docs: 'Recent Documents',
    by_status: 'By Status', by_dept: 'By Department', by_level: 'By Level',
    escalation_stats: 'Escalation Stats',
    assigned_to_me: 'Assigned to Me', pending_escalations: 'Pending',
    overdue_escalated: 'Overdue Escalated', resolved_escalations: 'Resolved',

    docs_title: 'My Files', all_docs: 'All Documents',
    upload_doc: 'Upload Document', doc_type: 'Document Type',
    file_size: 'File Size', checked_out: 'Checked Out', checked_in: 'Available',
    checkout: 'Check Out', checkin: 'Check In',
    admin_view: 'Admin View', no_docs: 'No documents found',
    send_approval: 'Send for Approval', archive_doc: 'Archive',
    favorites: 'Favorites', shared_with_me: 'Shared with Me',
    file_count: 'document(s)',

    library_title: 'Library', manage_folders: 'Manage Folders',
    all_files: 'All Files', new_folder: 'New Folder',
    folder_name: 'Folder Name', parent_folder: 'Parent Folder',
    root_folder: 'Root Folder', subfolder: 'Subfolder',
    upload_file: 'Upload File', file_name: 'File Name',
    drag_drop: 'Drag & drop file here or click to select',
    supported_types: 'PDF, DOCX, XLSX, PPTX, ZIP — up to 50MB',
    admin_folders_note: '🔐 Folders are managed by administrators',
    create_folder_btn: '+ Create New Folder',
    choose_folder: '— Select Folder —',
    empty_folder: 'No files from you in this folder',
    no_library_files: 'No files belong to you yet',
    upload_first: '📤 Upload First File',
    save_changes: 'Save Changes',
    add_folder_btn: '+ Add Folder',
    folder_management_title: 'Folder Structure Management',
    admin_permission_note: '🔐 System Admin permission only',
    subfolder_count: 'subfolder(s)',
    step_file: 'File', step_folder: 'Folder', step_details: 'Details',
    select_folder_label: 'Select Folder',
    select_folder_note: 'Choose the folder to save this file in',
    no_folders_msg: 'No folders in the library',
    no_folders_sub: 'Admin must create folders first',
    selected_path: 'Will be saved in:',
    file_name_label: 'File Name',
    file_name_no_ext: 'File name without extension',

    records_title: 'Institutional Records', new_record: 'New Record',
    record_number: 'Record No.', domain: 'Domain', record_type: 'Record Type',
    domain_legal: 'Legal & Contractual', domain_fin: 'Financial',
    domain_admin: 'Administrative', domain_hist: 'Historical & Archival',
    domain_res: 'Research & Academic',
    step_domain: 'Domain', step_type: 'Type',
    step_core: 'Core Data', step_domain_fields: 'Domain Fields',
    step_files: 'Attachments',
    choose_domain: 'Select Domain', choose_type: 'Record type in',
    no_extra_fields: 'No additional fields for this type',
    add_attachments: 'Add Attachments',
    drop_files: 'PDF, DOCX, XLSX, Images — up to 50MB each',
    file_ready: 'file(s) ready for upload',
    add_later: 'You can add attachments later',
    save_record: 'Save Record',
    record_created: 'Record created successfully',
    security_level: 'Security Level',
    filter_domain: 'All Domains', filter_status: 'All Statuses',
    submit_for_review: 'Submit for Review',
    approve_record: 'Approve', reject_record: 'Reject', archive_record: 'Archive',
    domain_specific: 'data',
    keywords: 'Keywords',
    no_records: 'No records found',
    create_first_record: '+ Create First Record',
    records_system: 'Metadata-driven records platform',
    kpi_total: 'Total Records', kpi_draft: 'Draft',
    kpi_review: 'Under Review', kpi_approved: 'Approved',

    tasks_title: 'Task Management', new_task: 'New Task',
    task_title_field: 'Task Title', task_desc: 'Detailed Description',
    assign_to: 'Assign To', choose_dept: '— Select Department —',
    choose_employee: '— Select Employee —', choose_priority: 'Priority',
    task_tags: 'Tags (comma separated)',
    tags_placeholder: 'training, finance, urgent, ...',
    title_placeholder: 'Clear and concise title...',
    desc_placeholder: 'Detailed task description and expected outcomes...',
    date_placeholder: 'DD/MM/YYYY',
    save_edits: 'Save Changes', create_task: 'Create Task', edit_task: 'Edit Task',
    status_actions: 'Actions', status_history: 'Status History',
    start_task: 'Start Task', send_review: 'Send for Review',
    close_approve: 'Close ✅', cancel_task: 'Cancel Task',
    escalate: 'Escalate', escalate_to_mgr: 'Escalate to Manager',
    add_comment: 'Add Comment', add_attachment: 'Add',
    comment_placeholder: 'Write your comment...',
    no_attachments: 'No attachments', no_comments: 'No comments',
    task_created_by: 'Created by', task_assigned_to: 'Assigned',
    task_created_at: 'Created on',
    board_view: 'Board', list_view: 'List',
    reports: 'Reports', tasks_reports_title: 'Task Reports',
    filter_all_status: 'All Statuses', filter_all_prio: 'All Priorities',
    filter_all_dept: 'All Departments', search_tasks: 'Search...',
    no_tasks: 'No tasks found',
    task_kpi_total: 'Total Tasks', task_kpi_inprog: 'In Progress',
    task_kpi_overdue: 'Overdue', task_kpi_done: 'Completed',
    task_kpi_escalated: 'Escalated',

    escalate_task: 'Escalate Task', your_role: 'Your Role',
    escalated_task_label: 'Escalated Task:',
    escalate_to_label: 'Escalate To',
    choose_person: '— Select Responsible Person —',
    escalation_reason_label: 'Escalation Reason',
    escalation_reason_ph: 'Explain clearly why this task needs to be escalated...',
    escalation_rules: 'Escalation Rules:',
    esc_rule1: 'Escalation does not automatically change task status',
    esc_rule2: 'Will be recorded in the audit log',
    esc_rule3: 'The escalation target will receive an immediate notification',
    confirm_escalation: 'Confirm Escalation', escalating: 'Escalating...',
    escalation_history: 'Escalation History',
    esc_level: 'Level',
    esc_pending: 'Pending', esc_accepted: 'Accepted', esc_resolved: 'Resolved',
    esc_reason_label: 'Reason:',
    level1: 'Level 1: To Supervisor',
    level2: 'Level 2: To Department Manager',
    level3: 'Level 3: Cross-Department',
    no_valid_targets: 'No valid escalation targets for your role',

    search_title: 'Advanced Search',
    search_ph: 'Search documents, records, knowledge assets...',
    search_btn: 'Search', search_scope: 'Search all Darah content',
    no_results: 'No results found', no_results_for: 'No results for',
    try_other: 'Try different keywords or check spelling',
    results_for: 'result(s) for', sort_relevance: 'Relevance ↓',
    sort_by: 'Sort by:', search_history: 'Recent Searches',
    clear_history: 'Clear', quick_search: 'Quick Search',
    search_empty_title: 'Search all Darah content',

    users_title: 'User Management', new_user: 'New User',
    full_name: 'Full Name', user_email: 'Email',
    user_dept: 'Department', user_role: 'Role',
    user_status: 'Status', user_actions: 'Actions',
    search_users: 'Search users...',
    choose_dept: '— Select Department —', choose_role: 'User',
    export_csv: 'Export CSV',
    edit_user_title: 'Edit User', add_user_title: 'Add New User',
    grant_access: '🔐 Grant Access', full_access: '🔓 Full Access',
    enable_user: 'Enable', disable_user: 'Disable', delete_user: 'Delete',
    user_inactive: 'Disabled',
    filter_all: 'All', filter_active: 'Active', filter_inactive: 'Inactive',
    role_viewer: 'Viewer', role_employee: 'Employee', role_supervisor: 'Supervisor',
    role_manager: 'Department Manager', role_admin: 'System Admin',

    workflows_title: 'Workflows', new_workflow: 'Build Workflow',
    inbox_title: 'Task Inbox', my_workflows_title: 'My Workflows',
    workflow_name: 'Workflow Name', workflow_steps: 'Steps',
    send_for_approval: '📤 Send for Approval',
    no_inbox: 'No tasks in your inbox', no_workflows: 'No workflows yet',

    notifications: 'Notifications', mark_read: 'Mark All Read',
    no_notifs: 'No notifications',

    content_model_title: 'Institutional Content Model',

    pw_modal_title: 'Change Password',
    pw_modal_sub: 'We recommend changing it periodically for security',
    pw_current_label: 'Current Password', pw_new_label: 'New Password',
    pw_confirm_label: 'Confirm Password',
    pw_min_hint: 'At least 8 characters, uppercase, lowercase, numbers and symbols',
    pw_strength_label: 'Strength:',
    pw_change_btn: '🔑 Change Password', pw_saving: '⏳ Saving...',
  }
}

const LangContext = createContext(null)

export function LangProvider({ children }) {
  const [lang, setLangState] = useState(() => localStorage.getItem('ecm_lang') || 'ar')

  const apply = (l) => {
    document.documentElement.dir  = T[l].dir
    document.documentElement.lang = l
    document.documentElement.style.fontFamily = T[l].font
  }

  const setLang = (l) => {
    setLangState(l)
    localStorage.setItem('ecm_lang', l)
    apply(l)
    try {
      const tok = localStorage.getItem('ecm_token')
      if (tok) fetch('/api/v1/users/language', {
        method:'PUT',
        headers:{'Authorization':`Bearer ${tok}`,'Content-Type':'application/json'},
        body: JSON.stringify({language:l})
      }).catch(()=>{})
    } catch {}
  }

  useEffect(() => apply(lang), [lang])

  const t  = (key) => T[lang]?.[key] ?? T.ar[key] ?? key
  const fmtDate = (d) => { try { return d ? new Date(d).toLocaleDateString(T[lang].dateLocale) : '—' } catch { return d||'—' } }
  const fmtNum  = (n) => { try { return n!=null ? new Intl.NumberFormat(T[lang].numLocale).format(n) : '—' } catch { return String(n) } }
  const isRTL = lang === 'ar'

  return React.createElement(LangContext.Provider, { value:{lang,setLang,t,isRTL,fmtDate,fmtNum} }, children)
}

export function useLang() {
  const ctx = useContext(LangContext)
  if (!ctx) throw new Error('useLang must be used inside LangProvider')
  return ctx
}

export const getLangHeader = () => ({ 'Accept-Language': localStorage.getItem('ecm_lang')||'ar' })
