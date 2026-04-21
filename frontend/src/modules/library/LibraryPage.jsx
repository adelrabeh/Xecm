import React, { useState, useRef } from 'react'
import { useFolderStore, useLibraryFilesV2 } from '../../hooks/useFolderStore'
import { useAuthStore }   from '../../store/authStore'
import { PreviewModal }   from '../../components/PreviewModal'
import { ShareModal }     from '../../components/ShareModal'
import { useToast }       from '../../components/Toast'
import client             from '../../api/client'

// ─── Constants ────────────────────────────────────────────────────────────────
const TYPE_ICON  = { PDF:'📕', DOCX:'📘', XLSX:'📗', PPTX:'📙', ZIP:'📦', PNG:'🖼', JPG:'🖼' }
const CLASS_CLS  = {
  'عام':        'bg-green-50 text-green-700 border-green-200',
  'داخلي':      'bg-blue-50 text-blue-700 border-blue-200',
  'سري':        'bg-orange-50 text-orange-700 border-orange-200',
  'سري للغاية': 'bg-red-50 text-red-700 border-red-200',
}
const STATUS_LBL = { Active:'نشط', Approved:'معتمد', Draft:'مسودة', Archived:'مؤرشف', UnderReview:'قيد المراجعة' }
const STATUS_CLS = { Active:'bg-green-100 text-green-700', Approved:'bg-blue-100 text-blue-700', Draft:'bg-gray-100 text-gray-600', Archived:'bg-gray-100 text-gray-500', UnderReview:'bg-yellow-100 text-yellow-700' }

// ─── Admin: Folder Manager Modal ──────────────────────────────────────────────
function FolderManagerModal({ folders, onClose, onSave }) {
  const [tree, setTree] = useState(folders.map(f => ({...f})))
  const [form, setForm] = useState({ name:'', parent:'', icon:'📁' })
  const [editId, setEditId] = useState(null)

  const FOLDER_ICONS = ['📁','📂','💻','💰','📋','📜','🔬','✉️','🗓','📑','🖼','🎓','🔒','📊','🤝','📱','🖧']

  const roots    = tree.filter(f => !f.parent)
  const childOf  = (id) => tree.filter(f => f.parent === id)
  const canDelete = (id) => childOf(id).length === 0

  const handleAdd = () => {
    if (!form.name.trim()) return
    const newId = 'f-' + Date.now()
    setTree(p => [...p, {
      id: newId, name: form.name.trim(),
      parent: form.parent || null,
      icon: form.icon, createdBy:'admin', count:0
    }])
    setForm({ name:'', parent:'', icon:'📁' })
  }

  const handleDelete = (id) => {
    if (!canDelete(id)) return
    setTree(p => p.filter(f => f.id !== id))
  }

  const handleRename = (id, newName) => {
    setTree(p => p.map(f => f.id === id ? {...f, name: newName} : f))
    setEditId(null)
  }

  const renderFolder = (folder, depth=0) => (
    <div key={folder.id}>
      <div className={`flex items-center gap-2 py-1.5 px-2 rounded-lg hover:bg-gray-50 group ${depth>0?'mr-'+depth*4:''}`}
        style={{marginRight: depth * 16}}>
        <span>{folder.icon}</span>
        {editId === folder.id ? (
          <input autoFocus defaultValue={folder.name}
            className="flex-1 border border-blue-300 rounded px-2 py-0.5 text-sm focus:outline-none"
            onBlur={e => handleRename(folder.id, e.target.value)}
            onKeyDown={e => { if(e.key==='Enter') handleRename(folder.id, e.target.value) }}/>
        ) : (
          <span className="flex-1 text-sm font-medium text-gray-800">{folder.name}</span>
        )}
        <span className="text-[10px] text-gray-400">{childOf(folder.id).length} ملف فرعي</span>
        <div className="opacity-0 group-hover:opacity-100 flex gap-1">
          <button onClick={()=>setEditId(folder.id)} className="text-xs text-blue-500 hover:text-blue-700 px-1">✏️</button>
          {canDelete(folder.id) && (
            <button onClick={()=>handleDelete(folder.id)} className="text-xs text-red-400 hover:text-red-600 px-1">🗑️</button>
          )}
        </div>
      </div>
      {childOf(folder.id).map(child => renderFolder(child, depth + 1))}
    </div>
  )

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[85vh] flex flex-col" onClick={e=>e.stopPropagation()}>
        <div className="p-5 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
          <div>
            <h2 className="font-bold text-gray-900">إدارة هيكل المجلدات</h2>
            <p className="text-xs text-gray-400 mt-0.5">🔐 صلاحية مدير النظام فقط</p>
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
        </div>

        {/* Current tree */}
        <div className="flex-1 overflow-y-auto p-4 space-y-0.5">
          {roots.map(f => renderFolder(f))}
        </div>

        {/* Add folder form */}
        <div className="p-4 border-t border-gray-100 space-y-3 bg-gray-50 flex-shrink-0">
          <p className="text-xs font-bold text-gray-700">إضافة مجلد جديد</p>
          <div className="flex gap-2">
            {FOLDER_ICONS.map(ic => (
              <button key={ic} onClick={()=>setForm(p=>({...p,icon:ic}))}
                className={`text-lg p-1 rounded-lg transition-all ${form.icon===ic?'bg-blue-100 ring-2 ring-blue-400':'hover:bg-gray-100'}`}>
                {ic}
              </button>
            ))}
          </div>
          <div className="grid grid-cols-2 gap-2">
            <input value={form.name} onChange={e=>setForm(p=>({...p,name:e.target.value}))}
              onKeyDown={e=>e.key==='Enter'&&handleAdd()}
              placeholder="اسم المجلد *"
              className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
            <select value={form.parent} onChange={e=>setForm(p=>({...p,parent:e.target.value}))}
              className="border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
              <option value="">مجلد رئيسي</option>
              {tree.map(f => <option key={f.id} value={f.id}>{f.icon} {f.name}</option>)}
            </select>
          </div>
          <div className="flex gap-2">
            <button onClick={handleAdd} disabled={!form.name.trim()}
              className="flex-1 bg-blue-700 text-white py-2 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-40 transition-colors">
              + إضافة مجلد
            </button>
            <button onClick={()=>onSave(tree)}
              className="flex-1 bg-green-600 text-white py-2 rounded-xl text-sm font-bold hover:bg-green-700 transition-colors">
              ✅ حفظ التغييرات
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

// ─── Upload Modal (folder-aware) ──────────────────────────────────────────────
function UploadModal({ folders, onClose, defaultFolder, onSuccess }) {
  const [file, setFile]            = useState(null)
  const [selectedFolder, setSF]    = useState(defaultFolder||'')
  const [form, setForm]            = useState({ name:'', classification:'داخلي', tags:'' })
  const [drag, setDrag]            = useState(false)
  const [loading, setLoading]      = useState(false)
  const fileRef = useRef()

  const roots   = folders.filter(f => !f.parent)
  const childOf = (id) => folders.filter(f => f.parent === id)

  const renderOption = (folder, depth=0) => (
    <>
      <option key={folder.id} value={folder.id}>
        {'　'.repeat(depth)}{folder.icon} {folder.name}
      </option>
      {childOf(folder.id).map(c => renderOption(c, depth+1))}
    </>
  )

  const handleFile = (f) => {
    setFile(f)
    if (!form.name) setForm(p=>({...p, name: f.name.replace(/\.[^.]+$/,'')}))
  }

  const handleDrop = (e) => { e.preventDefault(); setDrag(false); handleFile(e.dataTransfer.files[0]) }

  const handleUpload = async () => {
    if (!file || !selectedFolder || !form.name.trim()) return
    setLoading(true)
    const ext = (file.name.split('.').pop()||'PDF').toUpperCase()
    const folder = folders.find(f=>f.id===selectedFolder)
    const displayName = form.name.trim() || file.name.replace(/\.[^.]+$/, '')
    const newFile = {
      id:           'up-'+Date.now(),
      titleAr:      displayName,
      title:        displayName,
      name:         displayName + '.' + ext.toLowerCase(),
      originalName: file.name,
      type:         ext,
      fileType:     ext,
      size:         (file.size/1024/1024).toFixed(2)+' MB',
      fileSize:     (file.size/1024/1024).toFixed(2)+' MB',
      folder:       selectedFolder,
      folderName:   folder?.name||'',
      owner:        'أنت',
      created:      new Date().toISOString().split('T')[0],
      modified:     new Date().toISOString().split('T')[0],
      createdAt:    new Date().toISOString().split('T')[0],
      version:      '1.0',
      classification: form.classification,
      tags:         form.tags.split('،').map(t=>t.trim()).filter(Boolean),
      thumb:        TYPE_ICON[ext]||'📄',
      status:       'Active',
      isFav:        false,
      isCheckedOut: false,
      likes:        0,
    }
    setLoading(false)
    onSuccess(newFile)
    onClose()
  }

  const canUpload = file && selectedFolder && form.name.trim()
  const selectedFolderObj = folders.find(f=>f.id===selectedFolder)

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md" onClick={e=>e.stopPropagation()}>
        {/* Header */}
        <div className="p-5 border-b border-gray-100 flex items-center justify-between">
          <div>
            <h2 className="font-bold text-gray-900">رفع ملف جديد</h2>
            {selectedFolderObj && (
              <p className="text-xs text-blue-600 mt-0.5">📁 {selectedFolderObj.name}</p>
            )}
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-lg hover:bg-gray-100 flex items-center justify-center text-gray-400 text-xl">✕</button>
        </div>

        <div className="p-5 space-y-4">
          {/* File drop zone */}
          {!file ? (
            <div onDrop={handleDrop} onDragOver={e=>{e.preventDefault();setDrag(true)}} onDragLeave={()=>setDrag(false)}
              onClick={()=>fileRef.current?.click()}
              className={`border-2 border-dashed rounded-2xl p-8 text-center cursor-pointer transition-all ${drag?'border-blue-400 bg-blue-50':'border-gray-200 hover:border-blue-300 hover:bg-gray-50'}`}>
              <input ref={fileRef} type="file" className="hidden"
                accept=".pdf,.docx,.xlsx,.pptx,.doc,.xls,.zip,.jpg,.png"
                onChange={e=>handleFile(e.target.files[0])}/>
              <div className="text-4xl mb-2">📁</div>
              <p className="font-semibold text-gray-700 text-sm">اسحب الملف هنا أو اضغط للاختيار</p>
              <p className="text-xs text-gray-400 mt-1">PDF, DOCX, XLSX, PPTX, ZIP — حتى 50MB</p>
            </div>
          ) : (
            <div className="flex items-center gap-3 p-3 bg-green-50 border border-green-200 rounded-xl">
              <span className="text-2xl">{TYPE_ICON[(file.name.split('.').pop()||'').toUpperCase()]||'📄'}</span>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-gray-800 truncate">{file.name}</p>
                <p className="text-xs text-gray-400">{(file.size/1024/1024).toFixed(2)} MB</p>
              </div>
              <button onClick={()=>setFile(null)} className="text-gray-300 hover:text-red-500">✕</button>
            </div>
          )}

          {/* Folder picker */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">
              المجلد <span className="text-red-400">*</span>
            </label>
            <select value={selectedFolder} onChange={e=>setSF(e.target.value)}
              className={`w-full border rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 ${!selectedFolder?'border-red-200':'border-gray-200'}`}>
              <option value="">— اختر المجلد —</option>
              {roots.map(f => renderOption(f))}
            </select>
          </div>

          {/* File name */}
          <div>
            <label className="block text-xs font-bold text-gray-700 mb-1.5">اسم الملف</label>
            <input value={form.name} onChange={e=>setForm(p=>({...p,name:e.target.value}))}
              placeholder="اسم الملف بدون الامتداد"
              className="w-full border border-gray-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
          </div>

          {/* Classification + tags */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-1.5">التصنيف</label>
              <select value={form.classification} onChange={e=>setForm(p=>({...p,classification:e.target.value}))}
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400">
                {['عام','داخلي','سري','سري للغاية'].map(c=><option key={c}>{c}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-700 mb-1.5">الوسوم</label>
              <input value={form.tags} onChange={e=>setForm(p=>({...p,tags:e.target.value}))}
                placeholder="وسم1، وسم2"
                className="w-full border border-gray-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400 text-right"/>
            </div>
          </div>
        </div>

        <div className="p-5 border-t border-gray-100 flex gap-3">
          <button onClick={onClose} className="border border-gray-200 text-gray-600 px-4 py-2.5 rounded-xl text-sm hover:bg-gray-50">إلغاء</button>
          <button onClick={handleUpload} disabled={!canUpload||loading}
            className="flex-1 bg-blue-700 text-white py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 disabled:opacity-40 flex items-center justify-center gap-2 transition-colors">
            {loading?'⏳ جارٍ الرفع...':'📤 رفع الملف'}
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Main Library Page ────────────────────────────────────────────────────────
export default function LibraryPage() {
  const { show, ToastContainer }   = useToast()
  const { user }                   = useAuthStore()
  const [folders, setFolders]      = useFolderStore()
  const [files, setFiles]          = useLibraryFilesV2()

  const isAdmin = (user?.permissions||[]).some(p => p === 'admin.*' || p === 'admin.library')

  const [currentFolder, setCF]     = useState(null)
  const [folderPath, setFolderPath] = useState([])
  const [expanded, setExpanded]    = useState(new Set())
  const [selected, setSelected]    = useState(null)
  const [search, setSearch]        = useState('')
  const [view, setView]            = useState('list')  // list | grid
  const [filter, setFilter]        = useState('all')
  const [previewFile, setPreviewFile] = useState(null)
  const [shareFile, setShareFile]  = useState(null)
  const [showUpload, setShowUpload] = useState(false)
  const [showFolderMgr, setShowFolderMgr] = useState(false)

  const safeFolders = Array.isArray(folders) ? folders : []
  const safeFiles   = Array.isArray(files) ? files : []

  const roots   = safeFolders.filter(f => !f.parent)
  const childOf = (id) => safeFolders.filter(f => f.parent === id)

  const navigate = (folderId) => {
    setCF(folderId)
    setSelected(null)
    if (!folderId) { setFolderPath([]); return }
    const path = []
    let cur = safeFolders.find(f=>f.id===folderId)
    while (cur) { path.unshift(cur); cur = cur.parent ? safeFolders.find(f=>f.id===cur.parent) : null }
    setFolderPath(path)
  }

  // Count files per folder (including subfolders)
  const countInFolder = (folderId) => {
    const direct = safeFiles.filter(f=>f.folder===folderId).length
    const childCount = childOf(folderId).reduce((s,c)=>s+countInFolder(c.id),0)
    return direct + childCount
  }

  // Files to display
  const displayed = safeFiles
    .filter(f => {
      if (currentFolder) return f.folder === currentFolder
      return true
    })
    .filter(f => {
      if (filter==='favorites') return f.isFav
      if (filter==='recent') return true
      return true
    })
    .filter(f => {
      if (!search) return true
      const q = search.toLowerCase()
      return (f.name||'').toLowerCase().includes(q) ||
             (f.owner||'').includes(q) ||
             (f.tags||[]).some(t=>t.includes(q))
    })

  const handleUploadSuccess = (newFile) => {
    setFiles(p => [newFile, ...(Array.isArray(p)?p:[])])
    show(`✅ تم رفع "${newFile.name}" في ${newFile.folderName||'المكتبة'}`, 'success')
    // Navigate to folder
    if (newFile.folder) navigate(newFile.folder)
  }

  const handleDeleteFile = (file) => {
    if (!isAdmin) { show('ليس لديك صلاحية الحذف', 'error'); return }
    setFiles(p => (Array.isArray(p)?p:[]).filter(f=>f.id!==file.id))
    setSelected(null)
    show(`تم حذف: ${file.name}`, 'success')
  }

  const toggleFav = (file) => {
    setFiles(p => (Array.isArray(p)?p:[]).map(f=>f.id===file.id?{...f,isFav:!f.isFav}:f))
    show(file.isFav?'إزالة من المفضلة':'تمت الإضافة للمفضلة', 'success')
  }

  const renderFolderTree = (folder, depth=0) => {
    const isActive = currentFolder === folder.id
    const children = childOf(folder.id)
    const isExpanded = expanded.has(folder.id)
    const count = countInFolder(folder.id)

    return (
      <div key={folder.id}>
        <div
          onClick={()=>{ navigate(folder.id); if(children.length) setExpanded(p=>{ const n=new Set(p); n.has(folder.id)?n.delete(folder.id):n.add(folder.id); return n }) }}
          className={`flex items-center gap-2 px-2 py-1.5 rounded-lg cursor-pointer transition-all group ${isActive?'bg-blue-100 text-blue-700':'hover:bg-gray-50 text-gray-700'}`}
          style={{paddingRight: 8 + depth*16}}>
          {children.length>0 && (
            <span className="text-gray-400 text-[10px] w-3">{isExpanded?'▼':'▶'}</span>
          )}
          {children.length===0 && <span className="w-3"/>}
          <span className="text-base">{folder.icon}</span>
          <span className={`flex-1 text-xs font-medium truncate ${isActive?'font-bold':''}`}>{folder.name}</span>
          {count>0&&<span className="text-[10px] text-gray-400 flex-shrink-0">{count}</span>}
        </div>
        {(isExpanded||isActive) && children.map(c => renderFolderTree(c, depth+1))}
      </div>
    )
  }

  const currentFolderObj = safeFolders.find(f=>f.id===currentFolder)

  return (
    <div className="flex h-full gap-0 overflow-hidden -m-6 ">
      <ToastContainer/>

      {previewFile && <PreviewModal file={previewFile} onClose={()=>setPreviewFile(null)}/>}
      {shareFile   && <ShareModal   file={shareFile}   onClose={()=>setShareFile(null)}/>}
      {showFolderMgr && isAdmin && (
        <FolderManagerModal
          folders={safeFolders}
          onClose={()=>setShowFolderMgr(false)}
          onSave={newTree=>{ setFolders(newTree); setShowFolderMgr(false); show('✅ تم حفظ هيكل المجلدات','success') }}
        />
      )}
      {showUpload && (
        <UploadModal
          folders={safeFolders}
          defaultFolder={currentFolder}
          onClose={()=>setShowUpload(false)}
          onSuccess={handleUploadSuccess}
        />
      )}

      {/* ── Sidebar: Folder Tree ── */}
      <div className="w-60 flex-shrink-0 bg-white border-l border-gray-100 flex flex-col overflow-hidden">
        {/* Sidebar header */}
        <div className="px-3 py-3 border-b border-gray-100 flex-shrink-0">
          <div className="flex items-center justify-between mb-2">
            <p className="text-xs font-black text-gray-500 uppercase tracking-wider">المجلدات</p>
            {isAdmin && (
              <button onClick={()=>setShowFolderMgr(true)}
                title="إدارة المجلدات"
                className="text-xs text-gray-400 hover:text-blue-600 transition-colors px-1 rounded hover:bg-blue-50">
                ⚙️
              </button>
            )}
          </div>
          {/* All files button */}
          <button onClick={()=>navigate(null)}
            className={`w-full flex items-center gap-2 px-2 py-1.5 rounded-lg text-xs font-medium transition-all ${!currentFolder?'bg-blue-100 text-blue-700 font-bold':'text-gray-600 hover:bg-gray-50'}`}>
            <span>📚</span>
            <span className="flex-1 text-right">كل الملفات</span>
            <span className="text-gray-400">{safeFiles.length}</span>
          </button>
        </div>

        {/* Folder tree */}
        <div className="flex-1 overflow-y-auto p-2 space-y-0.5">
          {roots.map(f => renderFolderTree(f))}
          {roots.length===0&&(
            <div className="text-center py-6 text-gray-400">
              <div className="text-2xl mb-1">📁</div>
              <p className="text-xs">{isAdmin?'ابدأ بإنشاء مجلد':'لا توجد مجلدات'}</p>
            </div>
          )}
        </div>

        {/* Admin note */}
        {isAdmin && (
          <div className="p-2 border-t border-gray-100 flex-shrink-0">
            <button onClick={()=>setShowFolderMgr(true)}
              className="w-full text-xs text-blue-600 hover:text-blue-800 py-1.5 rounded-lg hover:bg-blue-50 transition-colors font-medium">
              + إنشاء مجلد جديد
            </button>
          </div>
        )}
        {!isAdmin && (
          <div className="p-2 border-t border-gray-50 flex-shrink-0">
            <p className="text-[10px] text-gray-400 text-center">🔐 المجلدات تُدار من قِبَل الإدارة</p>
          </div>
        )}
      </div>

      {/* ── Main content ── */}
      <div className="flex-1 flex flex-col overflow-hidden bg-gray-50">
        {/* Toolbar */}
        <div className="bg-white border-b border-gray-100 px-5 py-3 flex items-center gap-3 flex-shrink-0">
          {/* Breadcrumb */}
          <div className="flex items-center gap-1 text-sm text-gray-500 flex-1 min-w-0">
            <button onClick={()=>navigate(null)} className="hover:text-blue-600 font-medium flex-shrink-0">📚 المكتبة</button>
            {folderPath.map((p,i)=>(
              <React.Fragment key={p.id}>
                <span className="text-gray-300">›</span>
                <button onClick={()=>navigate(p.id)}
                  className={`hover:text-blue-600 truncate ${i===folderPath.length-1?'font-bold text-gray-900':''}`}>
                  {p.icon} {p.name}
                </button>
              </React.Fragment>
            ))}
          </div>

          {/* Search */}
          <div className="relative w-48">
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-300 text-sm">🔍</span>
            <input value={search} onChange={e=>setSearch(e.target.value)}
              placeholder="بحث..."
              className="w-full pr-8 pl-3 py-1.5 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"/>
          </div>

          {/* View toggle */}
          <div className="flex border border-gray-200 rounded-xl overflow-hidden">
            <button onClick={()=>setView('list')} className={`px-2.5 py-1.5 text-sm ${view==='list'?'bg-gray-800 text-white':'text-gray-500 hover:bg-gray-50'}`}>☰</button>
            <button onClick={()=>setView('grid')} className={`px-2.5 py-1.5 text-sm ${view==='grid'?'bg-gray-800 text-white':'text-gray-500 hover:bg-gray-50'}`}>⊞</button>
          </div>

          {/* Upload button — all users */}
          <button onClick={()=>setShowUpload(true)}
            className="bg-blue-700 text-white text-sm px-4 py-2 rounded-xl hover:bg-blue-800 transition-colors flex items-center gap-1.5 font-bold shadow-sm">
            📤 رفع
          </button>
        </div>

        {/* Subfolder chips (if in a folder with children) */}
        {currentFolder && childOf(currentFolder).length>0 && (
          <div className="bg-white border-b border-gray-100 px-5 py-2 flex gap-2 overflow-x-auto flex-shrink-0">
            {childOf(currentFolder).map(sub=>(
              <button key={sub.id} onClick={()=>navigate(sub.id)}
                className="flex-shrink-0 flex items-center gap-1.5 px-3 py-1.5 bg-gray-50 hover:bg-blue-50 border border-gray-200 hover:border-blue-300 rounded-xl text-xs font-medium text-gray-700 hover:text-blue-700 transition-all">
                {sub.icon} {sub.name}
                <span className="text-gray-400 text-[10px]">({countInFolder(sub.id)})</span>
              </button>
            ))}
          </div>
        )}

        {/* Files area */}
        <div className="flex-1 overflow-y-auto p-5">
          {/* Empty state */}
          {displayed.length===0&&(
            <div className="flex flex-col items-center justify-center h-full text-gray-400 py-16">
              <div className="text-5xl mb-3">📂</div>
              <p className="font-semibold text-gray-600">
                {currentFolder ? `المجلد "${currentFolderObj?.name}" فارغ` : 'لا توجد ملفات'}
              </p>
              <button onClick={()=>setShowUpload(true)}
                className="mt-4 bg-blue-700 text-white px-5 py-2.5 rounded-xl text-sm font-bold hover:bg-blue-800 transition-colors">
                📤 رفع أول ملف
              </button>
            </div>
          )}

          {/* List view */}
          {view==='list' && displayed.length>0 && (
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
              <table className="w-full text-sm">
                <thead><tr className="bg-gray-50 border-b border-gray-100">
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400">الملف</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden md:table-cell">المجلد</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden lg:table-cell">الحالة</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400 hidden lg:table-cell">التصنيف</th>
                  <th className="px-4 py-3 text-right text-xs font-black text-gray-400">الإجراءات</th>
                </tr></thead>
                <tbody className="divide-y divide-gray-50">
                  {displayed.map(f => {
                    const folder = safeFolders.find(fl=>fl.id===f.folder)
                    return (
                      <tr key={f.id}
                        onClick={()=>setSelected(selected?.id===f.id?null:f)}
                        className={`cursor-pointer transition-colors ${selected?.id===f.id?'bg-blue-50':'hover:bg-gray-50'}`}>
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2.5">
                            <span className="text-xl flex-shrink-0">{TYPE_ICON[f.type]||'📄'}</span>
                            <div className="min-w-0">
                              <p className="font-semibold text-gray-800 truncate max-w-[200px]">{f.name}</p>
                              <p className="text-[10px] text-gray-400">{f.size} · v{f.version} · {f.owner}</p>
                            </div>
                          </div>
                        </td>
                        <td className="px-4 py-3 hidden md:table-cell">
                          {folder&&<span className="text-xs text-gray-500">{folder.icon} {folder.name}</span>}
                        </td>
                        <td className="px-4 py-3 hidden lg:table-cell">
                          <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${STATUS_CLS[f.status]||'bg-gray-100 text-gray-500'}`}>
                            {STATUS_LBL[f.status]||f.status}
                          </span>
                        </td>
                        <td className="px-4 py-3 hidden lg:table-cell">
                          <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium border ${CLASS_CLS[f.classification]||''}`}>
                            {f.classification}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-1.5">
                            <button onClick={e=>{e.stopPropagation();setPreviewFile(f)}}
                              className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-blue-600 transition-colors" title="معاينة">👁</button>
                            <button onClick={e=>{e.stopPropagation();toggleFav(f)}}
                              className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-yellow-500 transition-colors" title="مفضلة">
                              {f.isFav?'⭐':'☆'}
                            </button>
                            <button onClick={e=>{e.stopPropagation();setShareFile(f)}}
                              className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-purple-600 transition-colors" title="مشاركة">🔗</button>
                            {isAdmin && (
                              <button onClick={e=>{e.stopPropagation();handleDeleteFile(f)}}
                                className="p-1.5 rounded-lg hover:bg-red-50 text-gray-300 hover:text-red-500 transition-colors" title="حذف">🗑️</button>
                            )}
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}

          {/* Grid view */}
          {view==='grid' && displayed.length>0 && (
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
              {displayed.map(f => {
                const folder = safeFolders.find(fl=>fl.id===f.folder)
                return (
                  <div key={f.id}
                    onClick={()=>setSelected(selected?.id===f.id?null:f)}
                    className={`bg-white rounded-2xl border-2 p-4 cursor-pointer hover:shadow-md transition-all ${selected?.id===f.id?'border-blue-400 shadow-md':'border-gray-100 hover:border-gray-200'}`}>
                    <div className="flex items-start justify-between mb-3">
                      <span className="text-3xl">{TYPE_ICON[f.type]||'📄'}</span>
                      {f.isFav&&<span className="text-yellow-400 text-sm">⭐</span>}
                    </div>
                    <p className="text-xs font-bold text-gray-800 truncate mb-1">{f.name}</p>
                    <p className="text-[10px] text-gray-400 mb-2">{f.size}</p>
                    {folder&&<p className="text-[10px] text-gray-400">{folder.icon} {folder.name}</p>}
                    <div className="flex gap-1 mt-2">
                      <button onClick={e=>{e.stopPropagation();setPreviewFile(f)}} className="text-xs p-1 rounded hover:bg-gray-100 text-gray-400">👁</button>
                      <button onClick={e=>{e.stopPropagation();setShareFile(f)}} className="text-xs p-1 rounded hover:bg-gray-100 text-gray-400">🔗</button>
                      {isAdmin&&<button onClick={e=>{e.stopPropagation();handleDeleteFile(f)}} className="text-xs p-1 rounded hover:bg-red-50 text-gray-300">🗑️</button>}
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </div>

      {/* ── File detail panel ── */}
      {selected && (
        <div className="w-72 flex-shrink-0 bg-white border-r border-gray-100 flex flex-col overflow-hidden">
          <div className="p-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
            <p className="font-bold text-sm text-gray-900 truncate flex-1 ml-2">{selected.name}</p>
            <button onClick={()=>setSelected(null)} className="text-gray-300 hover:text-gray-600 flex-shrink-0">✕</button>
          </div>
          <div className="flex-1 overflow-y-auto p-4 space-y-3">
            <div className="flex justify-center py-4">
              <span className="text-6xl">{TYPE_ICON[selected.type]||'📄'}</span>
            </div>
            {[
              ['الاسم', selected.name],
              ['الحجم', selected.size],
              ['النوع', selected.type],
              ['الإصدار', `v${selected.version}`],
              ['التصنيف', selected.classification],
              ['الحالة', STATUS_LBL[selected.status]||selected.status],
              ['المالك', selected.owner],
              ['تاريخ الإنشاء', selected.created],
              ['المجلد', safeFolders.find(f=>f.id===selected.folder)?.name||'—'],
            ].map(([l,v])=>(
              <div key={l} className="flex justify-between text-xs py-1.5 border-b border-gray-50">
                <span className="text-gray-400">{l}</span>
                <span className="font-medium text-gray-800 text-right max-w-[130px] truncate">{v}</span>
              </div>
            ))}
            {selected.tags?.length>0&&(
              <div className="flex flex-wrap gap-1.5 pt-1">
                {selected.tags.map(t=><span key={t} className="bg-blue-50 text-blue-600 text-[10px] px-2 py-0.5 rounded-full">#{t}</span>)}
              </div>
            )}
          </div>
          <div className="p-3 border-t border-gray-100 flex flex-col gap-2 flex-shrink-0">
            <button onClick={()=>setPreviewFile(selected)}
              className="w-full bg-blue-700 text-white py-2 rounded-xl text-sm font-bold hover:bg-blue-800 transition-colors">
              👁 معاينة
            </button>
            <button onClick={()=>setShareFile(selected)}
              className="w-full border border-gray-200 text-gray-600 py-2 rounded-xl text-sm font-medium hover:bg-gray-50 transition-colors">
              🔗 مشاركة
            </button>
            {isAdmin && (
              <button onClick={()=>handleDeleteFile(selected)}
                className="w-full border border-red-200 text-red-600 py-2 rounded-xl text-sm font-medium hover:bg-red-50 transition-colors">
                🗑️ حذف
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
