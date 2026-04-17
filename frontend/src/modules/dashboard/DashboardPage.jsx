import React, { useState, useEffect } from 'react'
import client from '../../api/client'

const KPI_CARDS = [
  { label: 'إجمالي الوثائق', key: 'totalDocuments', icon: '📄', color: 'bg-blue-50 text-blue-700', mock: 1284 },
  { label: 'سير العمل النشطة', key: 'activeWorkflows', icon: '🔄', color: 'bg-green-50 text-green-700', mock: 47 },
  { label: 'المهام المعلقة', key: 'pendingTasks', icon: '⏳', color: 'bg-yellow-50 text-yellow-700', mock: 23 },
  { label: 'العناصر المتأخرة', key: 'overdueItems', icon: '⚠️', color: 'bg-red-50 text-red-700', mock: 8 },
]

const RECENT_ACTIVITY = [
  { type: 'upload', desc: 'تم رفع وثيقة جديدة: تقرير Q3 2026', user: 'أحمد المطيري', time: 'منذ 5 دقائق' },
  { type: 'approve', desc: 'تمت الموافقة على طلب إجازة #1042', user: 'سارة الغامدي', time: 'منذ 12 دقيقة' },
  { type: 'workflow', desc: 'بدء سير عمل: مراجعة عقد التوريد', user: 'محمد العتيبي', time: 'منذ 30 دقيقة' },
  { type: 'hold', desc: 'تم تطبيق الحجز القانوني على ملف #A-2026', user: 'فاطمة القحطاني', time: 'منذ ساعة' },
  { type: 'archive', desc: 'أُرشفت 15 وثيقة تلقائياً', user: 'النظام', time: 'منذ ساعتين' },
]

function KpiCard({ label, icon, color, value, loading }) {
  return (
    <div className="bg-white rounded-xl border border-gray-100 p-5 flex items-center gap-4 shadow-sm hover:shadow-md transition-shadow">
      <div className={`w-12 h-12 rounded-xl ${color} flex items-center justify-center text-2xl flex-shrink-0`}>
        {icon}
      </div>
      <div>
        <p className="text-gray-500 text-xs font-medium mb-1">{label}</p>
        {loading
          ? <div className="skeleton h-8 w-16 rounded" />
          : <p className="text-2xl font-bold text-gray-800">{value?.toLocaleString('ar-SA') ?? '—'}</p>
        }
      </div>
    </div>
  )
}

function ActivityIcon({ type }) {
  const map = { upload: '📤', approve: '✅', workflow: '🔄', hold: '🔒', archive: '📦' }
  return <span className="text-lg">{map[type] || '📌'}</span>
}

export default function DashboardPage() {
  const [stats, setStats] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const res = await client.get('/api/v1/dashboard/stats')
        setStats(res.data.data || res.data)
      } catch {
        // Use mock data when API not ready
        setStats({ totalDocuments: 1284, activeWorkflows: 47, pendingTasks: 23, overdueItems: 8 })
      } finally {
        setLoading(false)
      }
    }
    fetchStats()
  }, [])

  return (
    <div className="space-y-6 max-w-7xl">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">لوحة التحكم</h1>
          <p className="text-gray-500 text-sm mt-0.5">
            {new Date().toLocaleDateString('ar-SA', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
          </p>
        </div>
        <button className="bg-primary-700 text-white text-sm px-4 py-2 rounded-lg hover:bg-primary-900 transition-colors flex items-center gap-2">
          <span>+</span> وثيقة جديدة
        </button>
      </div>

      {/* KPI Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {KPI_CARDS.map(card => (
          <KpiCard
            key={card.key}
            label={card.label}
            icon={card.icon}
            color={card.color}
            value={stats?.[card.key] ?? card.mock}
            loading={loading}
          />
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Activity */}
        <div className="lg:col-span-2 bg-white rounded-xl border border-gray-100 shadow-sm">
          <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
            <h2 className="font-semibold text-gray-800">آخر النشاطات</h2>
            <button onClick={()=>window.location.href='/documents'} className="text-primary-700 text-xs hover:underline">عرض الكل</button>
          </div>
          <div className="divide-y divide-gray-50">
            {RECENT_ACTIVITY.map((item, i) => (
              <div key={i} className="flex items-start gap-3 px-5 py-3.5">
                <div className="w-8 h-8 bg-gray-50 rounded-lg flex items-center justify-center flex-shrink-0 mt-0.5">
                  <ActivityIcon type={item.type} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm text-gray-700 leading-snug">{item.desc}</p>
                  <p className="text-xs text-gray-400 mt-0.5">{item.user} · {item.time}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Quick actions */}
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm">
          <div className="px-5 py-4 border-b border-gray-100">
            <h2 className="font-semibold text-gray-800">إجراءات سريعة</h2>
          </div>
          <div className="p-4 space-y-2">
            {[
              { label: 'رفع وثيقة جديدة', icon: '📤', color: 'bg-blue-50 hover:bg-blue-100', href: '/documents' },
              { label: 'مراجعة المهام المعلقة', icon: '📋', color: 'bg-yellow-50 hover:bg-yellow-100', href: '/workflows' },
              { label: 'البحث في الأرشيف', icon: '🔍', color: 'bg-gray-50 hover:bg-gray-100', href: '/documents' },
              { label: 'تقرير الامتثال', icon: '📊', color: 'bg-green-50 hover:bg-green-100', href: '/records' },
            ].map((action, i) => (
              <a key={i} href={action.href}
                className={`flex items-center gap-3 px-4 py-3 rounded-lg ${action.color} transition-colors cursor-pointer`}>
                <span className="text-xl">{action.icon}</span>
                <span className="text-sm font-medium text-gray-700">{action.label}</span>
              </a>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
