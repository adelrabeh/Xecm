import { ChangePasswordModal } from '../components/ChangePasswordModal'
import React, { useState } from 'react'
import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

const NAV = [
  { to: '/dashboard', icon: '⊞', label: 'لوحة التحكم' },
  { to: '/documents', icon: '📄', label: 'الوثائق' },
  { to: '/workflows', icon: '✅', label: 'سير العمل' },
  { to: '/library', icon: '📚', label: 'المكتبة' },
    { to: '/content-model', icon: '🏛️', label: 'نموذج المحتوى' },
    { to: '/records', icon: '🗂', label: 'السجلات' },
  { to: '/admin', icon: '⚙️', label: 'الإدارة' },
]

export default function AppLayout() {
  const [collapsed, setCollapsed] = useState(false)
  const { user, logout } = useAuthStore()
  const [showChangePass, setShowChangePass] = React.useState(false)
  const navigate = useNavigate()

  const handleLogout = () => { logout(); navigate('/login') }

  return (
    <>
      {showChangePass && <ChangePasswordModal onClose={() => setShowChangePass(false)} />}
    <div className="flex h-screen bg-gray-50 overflow-hidden" dir="rtl">
      {/* Sidebar */}
      <aside className={`${collapsed ? 'w-16' : 'w-64'} bg-gov-900 text-white flex flex-col transition-all duration-300 flex-shrink-0`}
        style={{ background: '#0f2342' }}>
        {/* Logo */}
        <div className="flex items-center gap-3 px-4 py-5 border-b border-white/10">
          <div className="w-9 h-9 bg-primary-500 rounded-lg flex items-center justify-center flex-shrink-0">
            <span className="text-white font-bold text-sm">D</span>
          </div>
          {!collapsed && (
            <div>
              <p className="font-bold text-sm leading-tight">دارة الملك عبدالعزيز</p>
              <p className="text-white/50 text-xs">نظام ECM</p>
            </div>
          )}
        </div>

        {/* Nav */}
        <nav className="flex-1 py-4 overflow-y-auto">
          {NAV.map(item => (
            <NavLink key={item.to} to={item.to}
              className={({ isActive }) =>
                `flex items-center gap-3 px-4 py-3 mx-2 rounded-lg mb-1 transition-all text-sm
                ${isActive ? 'bg-primary-500 text-white' : 'text-white/70 hover:bg-white/10 hover:text-white'}`
              }>
              <span className="text-lg flex-shrink-0">{item.icon}</span>
              {!collapsed && <span className="font-medium">{item.label}</span>}
            </NavLink>
          ))}
        </nav>

        {/* User */}
        <div className="border-t border-white/10 p-3">
          {!collapsed ? (
            <div className="flex items-center gap-3">
              <div className="w-8 h-8 bg-primary-500 rounded-full flex items-center justify-center text-xs font-bold flex-shrink-0">
                {user?.username?.[0]?.toUpperCase() || 'U'}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-xs font-medium truncate">{user?.username || 'مستخدم'}</p>
              </div>
              <button onClick={() => setShowChangePass(true)} className="text-white/50 hover:text-white text-xs">🔑 كلمة المرور</button>
              <button onClick={handleLogout} className="text-white/50 hover:text-white text-xs">خروج</button>
            </div>
          ) : (
            <button onClick={handleLogout} className="w-full flex justify-center text-white/50 hover:text-white text-lg">↩</button>
          )}
        </div>
      </aside>

      {/* Main */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        {/* Header */}
        <header className="h-14 bg-white border-b border-gray-200 flex items-center px-6 gap-4 flex-shrink-0">
          <button onClick={() => setCollapsed(!collapsed)}
            className="text-gray-500 hover:text-gray-800 transition-colors">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
            </svg>
          </button>
          <div className="flex-1" />
          <button className="relative text-gray-500 hover:text-gray-800">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
            </svg>
            <span className="absolute -top-1 -left-1 w-4 h-4 bg-red-500 text-white text-xs rounded-full flex items-center justify-center">3</span>
          </button>
          <div className="w-8 h-8 bg-primary-500 rounded-full flex items-center justify-center text-white text-sm font-bold">
            {user?.username?.[0]?.toUpperCase() || 'U'}
          </div>
        </header>

        {/* Page content */}
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
    </>
  )
}
