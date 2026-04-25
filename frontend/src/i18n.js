// ─── Translation dictionary ───────────────────────────────────────────────────
export const translations = {
  ar: {
    dir: 'rtl', lang: 'ar',
    // Nav
    nav_dashboard: 'لوحة التحكم', nav_tasks: 'المهام', nav_myfiles: 'ملفاتي',
    nav_workflows: 'سير العمل', nav_library: 'المكتبة', nav_records: 'السجلات',
    nav_content_model: 'نموذج المحتوى', nav_search: 'البحث', nav_admin: 'الإدارة',
    // Common
    save: 'حفظ', cancel: 'إلغاء', delete: 'حذف', edit: 'تعديل', add: 'إضافة',
    search: 'بحث', filter: 'تصفية', close: 'إغلاق', confirm: 'تأكيد',
    loading: 'جارٍ التحميل...', no_data: 'لا توجد بيانات', back: 'رجوع',
    yes: 'نعم', no: 'لا', all: 'الكل', total: 'الإجمالي',
    created_at: 'تاريخ الإنشاء', updated_at: 'آخر تعديل',
    name: 'الاسم', email: 'البريد الإلكتروني', role: 'الدور', dept: 'القسم',
    status: 'الحالة', priority: 'الأولوية', due_date: 'تاريخ الاستحقاق',
    description: 'الوصف', tags: 'الوسوم', classification: 'التصنيف',
    // Auth
    login: 'تسجيل الدخول', logout: 'تسجيل الخروج',
    username: 'اسم المستخدم', password: 'كلمة المرور',
    change_password: 'تغيير كلمة المرور',
    // Dashboard
    dashboard_title: 'لوحة التحكم', welcome: 'مرحباً',
    total_docs: 'إجمالي الوثائق', pending_tasks: 'المهام المعلقة',
    // Tasks
    tasks_title: 'إدارة المهام', new_task: 'مهمة جديدة',
    task_title: 'عنوان المهمة', assign_to: 'تكليف إلى',
    status_new: 'جديدة', status_assigned: 'مُسنَدة',
    status_inprogress: 'قيد التنفيذ', status_review: 'قيد المراجعة',
    status_completed: 'مكتملة', status_overdue: 'متأخرة', status_cancelled: 'ملغاة',
    prio_low: 'منخفضة', prio_medium: 'متوسطة', prio_high: 'عالية', prio_urgent: 'عاجلة',
    escalate: 'تصعيد', add_comment: 'إضافة تعليق', upload_attachment: 'رفع مرفق',
    // Documents
    docs_title: 'ملفاتي', upload: 'رفع', download: 'تنزيل', preview: 'معاينة',
    share: 'مشاركة', version: 'الإصدار', owner: 'المالك',
    // Library
    library_title: 'المكتبة', folder: 'مجلد', folders: 'المجلدات',
    new_folder: 'مجلد جديد', upload_file: 'رفع ملف',
    // Records
    records_title: 'السجلات المؤسسية', new_record: 'سجل جديد',
    domain: 'النطاق', record_type: 'نوع السجل', record_number: 'رقم السجل',
    // Search
    search_title: 'البحث المتقدم', search_placeholder: 'ابحث في المحتوى...',
    search_results: 'نتائج البحث', no_results: 'لا توجد نتائج',
    // Users
    users_title: 'إدارة المستخدمين', new_user: 'مستخدم جديد',
    active: 'فعال', inactive: 'معطل', grant_access: 'منح وصول',
    // Notifications
    notifications: 'الإشعارات', mark_read: 'تحديد كمقروء',
    // Errors
    required: 'هذا الحقل مطلوب', invalid_email: 'بريد إلكتروني غير صحيح',
    session_expired: 'انتهت الجلسة، يرجى تسجيل الدخول',
  },
  en: {
    dir: 'ltr', lang: 'en',
    // Nav
    nav_dashboard: 'Dashboard', nav_tasks: 'Tasks', nav_myfiles: 'My Files',
    nav_workflows: 'Workflows', nav_library: 'Library', nav_records: 'Records',
    nav_content_model: 'Content Model', nav_search: 'Search', nav_admin: 'Admin',
    // Common
    save: 'Save', cancel: 'Cancel', delete: 'Delete', edit: 'Edit', add: 'Add',
    search: 'Search', filter: 'Filter', close: 'Close', confirm: 'Confirm',
    loading: 'Loading...', no_data: 'No data found', back: 'Back',
    yes: 'Yes', no: 'No', all: 'All', total: 'Total',
    created_at: 'Created', updated_at: 'Updated',
    name: 'Name', email: 'Email', role: 'Role', dept: 'Department',
    status: 'Status', priority: 'Priority', due_date: 'Due Date',
    description: 'Description', tags: 'Tags', classification: 'Classification',
    // Auth
    login: 'Sign In', logout: 'Sign Out',
    username: 'Username', password: 'Password',
    change_password: 'Change Password',
    // Dashboard
    dashboard_title: 'Dashboard', welcome: 'Welcome',
    total_docs: 'Total Documents', pending_tasks: 'Pending Tasks',
    // Tasks
    tasks_title: 'Task Management', new_task: 'New Task',
    task_title: 'Task Title', assign_to: 'Assign To',
    status_new: 'New', status_assigned: 'Assigned',
    status_inprogress: 'In Progress', status_review: 'Under Review',
    status_completed: 'Completed', status_overdue: 'Overdue', status_cancelled: 'Cancelled',
    prio_low: 'Low', prio_medium: 'Medium', prio_high: 'High', prio_urgent: 'Urgent',
    escalate: 'Escalate', add_comment: 'Add Comment', upload_attachment: 'Upload Attachment',
    // Documents
    docs_title: 'My Files', upload: 'Upload', download: 'Download', preview: 'Preview',
    share: 'Share', version: 'Version', owner: 'Owner',
    // Library
    library_title: 'Library', folder: 'Folder', folders: 'Folders',
    new_folder: 'New Folder', upload_file: 'Upload File',
    // Records
    records_title: 'Institutional Records', new_record: 'New Record',
    domain: 'Domain', record_type: 'Record Type', record_number: 'Record No.',
    // Search
    search_title: 'Advanced Search', search_placeholder: 'Search content...',
    search_results: 'Search Results', no_results: 'No results found',
    // Users
    users_title: 'User Management', new_user: 'New User',
    active: 'Active', inactive: 'Inactive', grant_access: 'Grant Access',
    // Notifications
    notifications: 'Notifications', mark_read: 'Mark All Read',
    // Errors
    required: 'This field is required', invalid_email: 'Invalid email address',
    session_expired: 'Session expired, please sign in again',
  }
}

// ─── React context ─────────────────────────────────────────────────────────
import { createContext, useContext, useState, useEffect } from 'react'
import React from 'react'

const LangContext = createContext(null)

export function LangProvider({ children }) {
  const [lang, setLangState] = useState(() =>
    localStorage.getItem('ecm_lang') || 'ar'
  )

  const setLang = (l) => {
    setLangState(l)
    localStorage.setItem('ecm_lang', l)
    document.documentElement.dir  = translations[l].dir
    document.documentElement.lang = l
  }

  useEffect(() => {
    document.documentElement.dir  = translations[lang].dir
    document.documentElement.lang = lang
  }, [lang])

  const t = (key) => translations[lang][key] || translations.ar[key] || key
  const isRTL = lang === 'ar'

  return React.createElement(LangContext.Provider, { value: { lang, setLang, t, isRTL } }, children)
}

export function useLang() {
  return useContext(LangContext)
}
