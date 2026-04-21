// Shared folder store — admin creates, all users see and upload to
import { useLocalStorage } from './useLocalStorage'

const FOLDERS_KEY = 'ecm_folder_tree'
const FILES_KEY   = 'ecm_library_files_v2'

const DEFAULT_FOLDERS = [
  { id:'f1', name:'التحول الرقمي',         parent:null, icon:'💻', createdBy:'admin', count:0 },
  { id:'f2', name:'الشؤون المالية',         parent:null, icon:'💰', createdBy:'admin', count:0 },
  { id:'f3', name:'الشؤون الإدارية',        parent:null, icon:'📋', createdBy:'admin', count:0 },
  { id:'f4', name:'التاريخ والأرشيف',        parent:null, icon:'📜', createdBy:'admin', count:0 },
  { id:'f5', name:'الأبحاث والدراسات',       parent:null, icon:'🔬', createdBy:'admin', count:0 },
  { id:'f1-1', name:'التطبيقات',            parent:'f1', icon:'📱', createdBy:'admin', count:0 },
  { id:'f1-2', name:'البنية التحتية',        parent:'f1', icon:'🖧',  createdBy:'admin', count:0 },
  { id:'f1-3', name:'الأمن السيبراني',       parent:'f1', icon:'🔒', createdBy:'admin', count:0 },
  { id:'f2-1', name:'الميزانيات',            parent:'f2', icon:'📊', createdBy:'admin', count:0 },
  { id:'f2-2', name:'العقود والاتفاقيات',    parent:'f2', icon:'🤝', createdBy:'admin', count:0 },
  { id:'f3-1', name:'المراسلات الرسمية',     parent:'f3', icon:'✉️',  createdBy:'admin', count:0 },
  { id:'f3-2', name:'محاضر الاجتماعات',      parent:'f3', icon:'🗓',  createdBy:'admin', count:0 },
  { id:'f3-3', name:'السياسات والإجراءات',   parent:'f3', icon:'📑', createdBy:'admin', count:0 },
  { id:'f4-1', name:'المخطوطات',             parent:'f4', icon:'📜', createdBy:'admin', count:0 },
  { id:'f4-2', name:'الصور التاريخية',        parent:'f4', icon:'🖼', createdBy:'admin', count:0 },
  { id:'f5-1', name:'الأوراق البحثية',        parent:'f5', icon:'🔬', createdBy:'admin', count:0 },
  { id:'f5-2', name:'الرسائل العلمية',        parent:'f5', icon:'🎓', createdBy:'admin', count:0 },
]

const SEED_FILES = [
  { id:'d1', name:'تقرير الميزانية السنوي 2026.pdf', type:'PDF',  size:'2.4 MB', folder:'f2-1', owner:'أحمد الزهراني', created:'2026-01-15', classification:'سري',    version:'3.1', tags:['مالي','2026'],   thumb:'📕', status:'Approved', isFav:true,  likes:5,  isCheckedOut:false },
  { id:'d2', name:'عقد توريد المستلزمات 2026.docx', type:'DOCX', size:'1.2 MB', folder:'f2-2', owner:'مريم العنزي',  created:'2026-02-10', classification:'داخلي', version:'2.0', tags:['عقود'],          thumb:'📘', status:'UnderReview',isFav:false, likes:2,  isCheckedOut:true },
  { id:'d3', name:'سياسة حماية البيانات.pdf',       type:'PDF',  size:'0.9 MB', folder:'f3-3', owner:'خالد القحطاني',created:'2025-11-01', classification:'عام',   version:'4.0', tags:['سياسات'],        thumb:'📕', status:'Active',     isFav:true,  likes:8,  isCheckedOut:false },
  { id:'d4', name:'محضر اجتماع مجلس الإدارة.pdf',  type:'PDF',  size:'3.1 MB', folder:'f3-2', owner:'فاطمة الشمري', created:'2026-03-20', classification:'سري',    version:'1.0', tags:['محاضر','Q1'],    thumb:'📕', status:'Active',     isFav:true,  likes:3,  isCheckedOut:false },
  { id:'d5', name:'خطة التحول الرقمي 2026.pptx',   type:'PPTX', size:'8.2 MB', folder:'f1',   owner:'نورة السبيعي', created:'2026-03-15', classification:'داخلي', version:'1.5', tags:['استراتيجي'],     thumb:'📙', status:'Active',     isFav:true,  likes:15, isCheckedOut:false },
  { id:'d6', name:'تطبيق نظام المحتوى.docx',        type:'DOCX', size:'1.8 MB', folder:'f1-1', owner:'عمر الدوسري',  created:'2026-04-01', classification:'داخلي', version:'0.3', tags:['تطبيقات'],       thumb:'📘', status:'Draft',      isFav:false, likes:0,  isCheckedOut:false },
  { id:'d7', name:'مخطوطة تاريخ نجد القرن 12.pdf', type:'PDF',  size:'4.7 MB', folder:'f4-1', owner:'أحمد الزهراني', created:'2025-12-01', classification:'عام',   version:'2.0', tags:['تاريخ'],         thumb:'📕', status:'Archived',   isFav:false, likes:1,  isCheckedOut:false },
  { id:'d8', name:'دراسة العمارة النجدية.pdf',       type:'PDF',  size:'2.1 MB', folder:'f5-1', owner:'خالد القحطاني',created:'2025-06-01', classification:'عام',   version:'5.0', tags:['أبحاث','تراث'],  thumb:'📕', status:'Active',     isFav:false, likes:6,  isCheckedOut:false },
]

export function useFolderStore() {
  return useLocalStorage(FOLDERS_KEY, DEFAULT_FOLDERS)
}

export function useLibraryFilesV2() {
  return useLocalStorage(FILES_KEY, SEED_FILES)
}

export function addToLibraryV2(file) {
  try {
    const existing = JSON.parse(localStorage.getItem(FILES_KEY) || 'null')
    const arr = Array.isArray(existing) ? existing : SEED_FILES
    const updated = [file, ...arr.filter(f => f.id !== file.id)]
    localStorage.setItem(FILES_KEY, JSON.stringify(updated))
  } catch {}
}

export function DEFAULT_FOLDER_TREE() { return DEFAULT_FOLDERS }
