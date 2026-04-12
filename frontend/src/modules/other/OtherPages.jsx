import React from 'react'

export function RecordsPage() {
  const records = [
    { id: 1, title: 'سجل العقود الحكومية 2025', retention: '10 سنوات', status: 'Active', count: 342 },
    { id: 2, title: 'سجل الموارد البشرية', retention: '7 سنوات', status: 'Active', count: 1205 },
    { id: 3, title: 'سجل المراسلات الرسمية', retention: '5 سنوات', status: 'Active', count: 8732 },
    { id: 4, title: 'سجل العقود المنتهية 2020', retention: '10 سنوات', status: 'LegalHold', count: 89 },
  ]

  return (
    <div className="space-y-4 max-w-7xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">إدارة السجلات</h1>
          <p className="text-gray-500 text-sm">جداول الاحتفاظ والأرشفة</p>
        </div>
        <button className="bg-primary-700 text-white text-sm px-4 py-2 rounded-lg hover:bg-primary-900">
          + سجل جديد
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {[
          { label: 'سجلات نشطة', value: '4', icon: '📂', color: 'bg-green-50 text-green-700' },
          { label: 'حجز قانوني', value: '1', icon: '🔒', color: 'bg-red-50 text-red-700' },
          { label: 'بانتظار الإتلاف', value: '0', icon: '🗑', color: 'bg-gray-50 text-gray-600' },
        ].map((k, i) => (
          <div key={i} className="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
            <div className={`w-10 h-10 rounded-lg ${k.color} flex items-center justify-center text-xl`}>{k.icon}</div>
            <div>
              <p className="text-xs text-gray-500">{k.label}</p>
              <p className="text-2xl font-bold text-gray-800">{k.value}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-50 border-b border-gray-100">
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">السجل</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">فترة الاحتفاظ</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">الحالة</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">عدد الوثائق</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">الإجراءات</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {records.map(r => (
              <tr key={r.id} className="hover:bg-gray-50">
                <td className="px-4 py-3 font-medium text-gray-800">{r.title}</td>
                <td className="px-4 py-3 text-gray-500">{r.retention}</td>
                <td className="px-4 py-3">
                  <span className={`text-xs px-2.5 py-1 rounded-full font-medium
                    ${r.status === 'LegalHold' ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
                    {r.status === 'LegalHold' ? 'حجز قانوني' : 'نشط'}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-600">{r.count.toLocaleString('ar-SA')}</td>
                <td className="px-4 py-3">
                  <button className="text-xs text-primary-700 hover:underline ml-3">عرض</button>
                  <button className="text-xs text-gray-500 hover:underline">تصدير</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

export function AdminPage() {
  const users = [
    { id: 1, name: 'أحمد المطيري', username: 'a.mutairi', role: 'مدير النظام', status: 'Active', dept: 'تقنية المعلومات' },
    { id: 2, name: 'سارة الغامدي', username: 's.ghamdi', role: 'محرر', status: 'Active', dept: 'الشؤون القانونية' },
    { id: 3, name: 'محمد العتيبي', username: 'm.otaibi', role: 'قارئ', status: 'Active', dept: 'الموارد البشرية' },
    { id: 4, name: 'فاطمة القحطاني', username: 'f.qahtani', role: 'محرر', status: 'Inactive', dept: 'المالية' },
  ]

  return (
    <div className="space-y-4 max-w-7xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">لوحة الإدارة</h1>
          <p className="text-gray-500 text-sm">المستخدمون والصلاحيات</p>
        </div>
        <button className="bg-primary-700 text-white text-sm px-4 py-2 rounded-lg hover:bg-primary-900">
          + مستخدم جديد
        </button>
      </div>

      <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <h2 className="font-semibold text-gray-800">المستخدمون</h2>
          <input
            type="text"
            placeholder="بحث..."
            className="border border-gray-200 rounded-lg px-3 py-1.5 text-sm text-right focus:outline-none focus:ring-2 focus:ring-primary-500 w-48"
          />
        </div>
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-50 border-b border-gray-100">
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">المستخدم</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">القسم</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">الدور</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">الحالة</th>
              <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">إجراءات</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {users.map(u => (
              <tr key={u.id} className="hover:bg-gray-50">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 bg-primary-100 rounded-full flex items-center justify-center text-primary-700 font-medium text-xs">
                      {u.name[0]}
                    </div>
                    <div>
                      <p className="font-medium text-gray-800">{u.name}</p>
                      <p className="text-xs text-gray-400">@{u.username}</p>
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3 text-gray-600">{u.dept}</td>
                <td className="px-4 py-3">
                  <span className="text-xs bg-blue-50 text-blue-700 px-2.5 py-1 rounded-full">{u.role}</span>
                </td>
                <td className="px-4 py-3">
                  <span className={`text-xs px-2.5 py-1 rounded-full font-medium
                    ${u.status === 'Active' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>
                    {u.status === 'Active' ? 'نشط' : 'غير نشط'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <button className="text-xs text-primary-700 hover:underline ml-3">تعديل</button>
                  <button className="text-xs text-red-500 hover:underline">تعطيل</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
