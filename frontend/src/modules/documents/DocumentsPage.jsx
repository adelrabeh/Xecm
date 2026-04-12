import React, { useState, useEffect } from 'react'
import client from '../../api/client'

const MOCK_DOCS = [
  { id: 1, titleAr: 'تقرير الميزانية السنوي 2026', documentTypeId: 1, status: 'Active', createdAt: '2026-04-01', version: '1.2' },
  { id: 2, titleAr: 'عقد توريد المستلزمات المكتبية', documentTypeId: 2, status: 'UnderReview', createdAt: '2026-03-28', version: '2.0' },
  { id: 3, titleAr: 'سياسة حماية البيانات المحدثة', documentTypeId: 3, status: 'Approved', createdAt: '2026-03-25', version: '3.1' },
  { id: 4, titleAr: 'محضر اجتماع مجلس الإدارة Q1', documentTypeId: 1, status: 'Active', createdAt: '2026-03-20', version: '1.0' },
  { id: 5, titleAr: 'تقرير التدقيق الداخلي 2025', documentTypeId: 4, status: 'Archived', createdAt: '2026-01-15', version: '1.0' },
  { id: 6, titleAr: 'خطة الاستمرارية التشغيلية', documentTypeId: 3, status: 'Draft', createdAt: '2026-04-10', version: '0.3' },
]

const STATUS_MAP = {
  Active: { label: 'نشط', cls: 'bg-green-100 text-green-700' },
  UnderReview: { label: 'قيد المراجعة', cls: 'bg-yellow-100 text-yellow-700' },
  Approved: { label: 'معتمد', cls: 'bg-blue-100 text-blue-700' },
  Archived: { label: 'مؤرشف', cls: 'bg-gray-100 text-gray-600' },
  Draft: { label: 'مسودة', cls: 'bg-purple-100 text-purple-700' },
}

export default function DocumentsPage() {
  const [docs, setDocs] = useState(MOCK_DOCS)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [selected, setSelected] = useState(null)

  useEffect(() => {
    const fetchDocs = async () => {
      setLoading(true)
      try {
        const res = await client.get('/api/v1/documents', { params: { page: 1, pageSize: 20 } })
        const data = res.data?.data?.items || res.data?.data || res.data?.items
        if (Array.isArray(data) && data.length > 0) setDocs(data)
      } catch {
        // Keep mock data
      } finally {
        setLoading(false)
      }
    }
    fetchDocs()
  }, [])

  const filtered = docs.filter(d =>
    (d.titleAr || d.title || '').includes(search) || search === ''
  )

  return (
    <div className="space-y-4 max-w-7xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">إدارة الوثائق</h1>
          <p className="text-gray-500 text-sm">{filtered.length} وثيقة</p>
        </div>
        <div className="flex gap-2">
          <button className="border border-gray-300 text-gray-600 text-sm px-4 py-2 rounded-lg hover:bg-gray-50">
            📥 استيراد
          </button>
          <button className="bg-primary-700 text-white text-sm px-4 py-2 rounded-lg hover:bg-primary-900 flex items-center gap-2">
            + رفع وثيقة
          </button>
        </div>
      </div>

      {/* Search & filters */}
      <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4">
        <div className="flex gap-3">
          <div className="relative flex-1">
            <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400">🔍</span>
            <input
              type="text"
              placeholder="البحث في الوثائق..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pr-10 pl-4 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 text-right"
            />
          </div>
          <select className="border border-gray-200 rounded-lg px-3 py-2.5 text-sm text-gray-600 focus:outline-none focus:ring-2 focus:ring-primary-500">
            <option>جميع الأنواع</option>
            <option>تقارير</option>
            <option>عقود</option>
            <option>سياسات</option>
          </select>
          <select className="border border-gray-200 rounded-lg px-3 py-2.5 text-sm text-gray-600 focus:outline-none focus:ring-2 focus:ring-primary-500">
            <option>جميع الحالات</option>
            <option>نشط</option>
            <option>مؤرشف</option>
            <option>قيد المراجعة</option>
          </select>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-100">
                <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wide">الوثيقة</th>
                <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wide">الحالة</th>
                <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wide">الإصدار</th>
                <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wide">التاريخ</th>
                <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase tracking-wide">الإجراءات</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 5 }).map((_, j) => (
                      <td key={j} className="px-4 py-3">
                        <div className="skeleton h-4 rounded w-full" />
                      </td>
                    ))}
                  </tr>
                ))
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={5} className="text-center py-12 text-gray-400">
                    <div className="text-4xl mb-2">📭</div>
                    <p>لا توجد وثائق</p>
                  </td>
                </tr>
              ) : (
                filtered.map(doc => {
                  const status = STATUS_MAP[doc.status] || { label: doc.status, cls: 'bg-gray-100 text-gray-600' }
                  return (
                    <tr key={doc.id} className="hover:bg-gray-50 transition-colors cursor-pointer" onClick={() => setSelected(doc)}>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 bg-blue-50 rounded-lg flex items-center justify-center text-blue-600 flex-shrink-0 text-sm">
                            📄
                          </div>
                          <div>
                            <p className="font-medium text-gray-800 text-sm">{doc.titleAr || doc.title}</p>
                            <p className="text-xs text-gray-400">#{doc.id}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`text-xs px-2.5 py-1 rounded-full font-medium ${status.cls}`}>
                          {status.label}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-600">{doc.version || '1.0'}</td>
                      <td className="px-4 py-3 text-gray-500 text-xs">
                        {doc.createdAt ? new Date(doc.createdAt).toLocaleDateString('ar-SA') : '—'}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex gap-2">
                          <button className="text-xs text-primary-700 hover:underline" onClick={e => { e.stopPropagation(); setSelected(doc) }}>عرض</button>
                          <button className="text-xs text-gray-500 hover:underline">تعديل</button>
                        </div>
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-between text-sm text-gray-500">
          <span>عرض {Math.min(filtered.length, 20)} من {filtered.length} وثيقة</span>
          <div className="flex gap-1">
            <button className="px-3 py-1 rounded border border-gray-200 hover:bg-gray-50">السابق</button>
            <button className="px-3 py-1 rounded bg-primary-700 text-white">1</button>
            <button className="px-3 py-1 rounded border border-gray-200 hover:bg-gray-50">التالي</button>
          </div>
        </div>
      </div>

      {/* Detail modal */}
      {selected && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4" onClick={() => setSelected(null)}>
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-bold text-gray-900">تفاصيل الوثيقة</h2>
              <button onClick={() => setSelected(null)} className="text-gray-400 hover:text-gray-600 text-xl">✕</button>
            </div>
            <div className="space-y-3">
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500 text-sm">العنوان</span>
                <span className="font-medium text-sm">{selected.titleAr || selected.title}</span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500 text-sm">الحالة</span>
                <span className={`text-xs px-2.5 py-1 rounded-full font-medium ${(STATUS_MAP[selected.status] || {}).cls}`}>
                  {(STATUS_MAP[selected.status] || { label: selected.status }).label}
                </span>
              </div>
              <div className="flex justify-between py-2 border-b border-gray-100">
                <span className="text-gray-500 text-sm">الإصدار</span>
                <span className="font-medium text-sm">{selected.version}</span>
              </div>
              <div className="flex justify-between py-2">
                <span className="text-gray-500 text-sm">تاريخ الإنشاء</span>
                <span className="font-medium text-sm">{selected.createdAt}</span>
              </div>
            </div>
            <div className="flex gap-2 mt-6">
              <button className="flex-1 bg-primary-700 text-white py-2 rounded-lg text-sm hover:bg-primary-900">تنزيل</button>
              <button className="flex-1 border border-gray-300 text-gray-600 py-2 rounded-lg text-sm hover:bg-gray-50">مشاركة</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
