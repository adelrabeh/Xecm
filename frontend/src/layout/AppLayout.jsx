import { ChangePasswordModal } from '../components/ChangePasswordModal'
import React, { useState, useEffect } from 'react'
import { Outlet, NavLink, useNavigate, useLocation } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

// Bottom nav items (mobile) — most used pages
const BOTTOM_NAV = [
  { to: '/dashboard', icon: '⊞', label: 'الرئيسية' },
  { to: '/tasks',     icon: '📋', label: 'المهام' },
  { to: '/documents', icon: '📄', label: 'ملفاتي' },
  { to: '/library',   icon: '📚', label: 'المكتبة' },
  { to: '/workflows', icon: '✅', label: 'سير العمل' },
]

// Full sidebar nav (desktop + mobile drawer)
const FULL_NAV = [
  { to: '/dashboard',     icon: '⊞',  label: 'لوحة التحكم' },
  { to: '/tasks',         icon: '📋', label: 'المهام' },
  { to: '/documents',     icon: '📄', label: 'ملفاتي' },
  { to: '/workflows',     icon: '✅', label: 'سير العمل' },
  { to: '/library',       icon: '📚', label: 'المكتبة' },
  { to: '/records',       icon: '🗂',  label: 'السجلات' },
  { to: '/content-model', icon: '🏛️', label: 'نموذج المحتوى' },
  { to: '/search',        icon: '🔍', label: 'البحث' },
  { to: '/admin',         icon: '⚙️', label: 'الإدارة' },
]

export default function AppLayout() {
  const [collapsed, setCollapsed]     = useState(false)
  const [mobileMenuOpen, setMobileMenu] = useState(false)
  const [showChangePass, setShowChangePass] = useState(false)
  const [notifOpen, setNotifOpen]     = useState(false)
  const { user, logout } = useAuthStore()
  const navigate  = useNavigate()
  const location  = useLocation()

  // Close mobile menu on route change
  useEffect(() => { setMobileMenu(false) }, [location.pathname])

  const handleLogout = () => { logout(); navigate('/login') }

  const NOTIFS = [
    { id:1, icon:'📋', text:'تم تكليفك بمهمة جديدة: مراجعة عقود الربع الثاني', time:'منذ 5 دقائق', read:false, to:'/tasks' },
    { id:2, icon:'⚠️', text:'المهمة "تدقيق المشتريات" تجاوزت تاريخ الاستحقاق', time:'منذ ساعة',    read:false, to:'/tasks' },
    { id:3, icon:'💬', text:'تعليق جديد على: إعداد تقرير الأداء الشهري',          time:'منذ 2 ساعة', read:true,  to:'/tasks' },
  ]
  const unreadCount = NOTIFS.filter(n=>!n.read).length

  return (
    <>
      {showChangePass && <ChangePasswordModal onClose={() => setShowChangePass(false)} />}

      {/* ── Mobile menu overlay ── */}
      {mobileMenuOpen && (
        <div className="fixed inset-0 z-50 flex" dir="rtl">
          <div className="absolute inset-0 bg-black/60" onClick={()=>setMobileMenu(false)}/>
          <aside className="relative w-72 bg-white shadow-2xl flex flex-col h-full">
            {/* Header */}
            <div className="flex items-center gap-3 px-4 py-5 border-b" style={{background:'#0f2342'}}>
              <div className="w-9 h-9 bg-blue-500 rounded-xl flex items-center justify-center text-white font-black text-sm flex-shrink-0">D</div>
              <div>
                <p className="font-bold text-sm text-white">دارة الملك عبدالعزيز</p>
                <p className="text-white/50 text-xs">نظام ECM</p>
              </div>
              <button onClick={()=>setMobileMenu(false)} className="mr-auto text-white/60 hover:text-white text-2xl w-8 h-8 flex items-center justify-center">✕</button>
            </div>

            {/* User card */}
            <div className="px-4 py-3 bg-blue-50 border-b border-blue-100 flex items-center gap-3">
              <div className="w-10 h-10 bg-blue-600 rounded-full flex items-center justify-center text-white font-bold text-sm flex-shrink-0">
                {user?.username?.[0]?.toUpperCase() || 'U'}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-bold text-gray-900 truncate">{user?.fullNameAr || user?.username || 'مستخدم'}</p>
                <p className="text-xs text-gray-500">{user?.username}</p>
              </div>
            </div>

            {/* Nav */}
            <nav className="flex-1 py-3 overflow-y-auto px-2">
              {FULL_NAV.map(item => (
                <NavLink key={item.to} to={item.to}
                  className={({isActive}) =>
                    `flex items-center gap-3 px-4 py-3.5 rounded-xl mb-1 transition-all text-sm font-medium ${
                      isActive ? 'bg-blue-700 text-white' : 'text-gray-700 hover:bg-gray-100'
                    }`}>
                  <span className="text-xl flex-shrink-0">{item.icon}</span>
                  <span>{item.label}</span>
                </NavLink>
              ))}
            </nav>

            {/* Actions */}
            <div className="border-t p-3 space-y-2">
              <button onClick={()=>{setShowChangePass(true);setMobileMenu(false)}}
                className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors">
                🔑 <span>تغيير كلمة المرور</span>
              </button>
              <button onClick={handleLogout}
                className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium text-red-600 hover:bg-red-50 transition-colors">
                ↩ <span>تسجيل الخروج</span>
              </button>
            </div>
          </aside>
        </div>
      )}

      {/* ── Notification drawer ── */}
      {notifOpen && (
        <div className="fixed inset-0 z-40 flex items-start justify-end pt-14" dir="rtl" onClick={()=>setNotifOpen(false)}>
          <div className="w-full max-w-sm bg-white shadow-2xl rounded-bl-2xl overflow-hidden" onClick={e=>e.stopPropagation()}>
            <div className="px-4 py-3 border-b border-gray-100 flex items-center justify-between bg-gray-50">
              <p className="font-bold text-gray-900">الإشعارات</p>
              <span className="text-xs text-blue-600 cursor-pointer hover:underline">تحديد الكل كمقروء</span>
            </div>
            {NOTIFS.map(n=>(
              <div key={n.id} onClick={()=>{navigate(n.to);setNotifOpen(false)}}
                className={`flex items-start gap-3 px-4 py-3.5 cursor-pointer hover:bg-gray-50 border-b border-gray-50 transition-colors ${!n.read?'bg-blue-50/60':''}`}>
                <span className="text-xl flex-shrink-0 mt-0.5">{n.icon}</span>
                <div className="flex-1 min-w-0">
                  <p className={`text-sm leading-snug ${!n.read?'font-semibold text-gray-900':'text-gray-600'}`}>{n.text}</p>
                  <p className="text-[10px] text-gray-400 mt-1">{n.time}</p>
                </div>
                {!n.read && <div className="w-2 h-2 bg-blue-500 rounded-full flex-shrink-0 mt-1.5"/>}
              </div>
            ))}
            {NOTIFS.length===0 && <div className="py-10 text-center text-gray-400 text-sm">لا توجد إشعارات</div>}
          </div>
        </div>
      )}

      <div className="flex h-screen bg-gray-50 overflow-hidden" dir="rtl">

        {/* ── Desktop sidebar ── */}
        <aside className={`hidden md:flex ${collapsed?'w-16':'w-64'} flex-col transition-all duration-300 flex-shrink-0`}
          style={{background:'#0f2342'}}>
          <div className="flex items-center gap-3 px-4 py-5 border-b border-white/10">
            <div className="w-9 h-9 bg-blue-500 rounded-xl flex items-center justify-center flex-shrink-0">
              <span className="text-white font-black text-sm">D</span>
            </div>
            {!collapsed && (
              <div>
                <p className="font-bold text-sm text-white leading-tight">دارة الملك عبدالعزيز</p>
                <p className="text-white/50 text-xs">نظام ECM</p>
              </div>
            )}
          </div>

          <nav className="flex-1 py-4 overflow-y-auto">
            {FULL_NAV.map(item => (
              <NavLink key={item.to} to={item.to}
                className={({isActive}) =>
                  `flex items-center gap-3 px-4 py-3 mx-2 rounded-xl mb-1 transition-all text-sm ${
                    isActive ? 'bg-blue-600 text-white' : 'text-white/70 hover:bg-white/10 hover:text-white'
                  }`}>
                <span className="text-lg flex-shrink-0">{item.icon}</span>
                {!collapsed && <span className="font-medium truncate">{item.label}</span>}
              </NavLink>
            ))}
          </nav>

          <div className="border-t border-white/10 p-3">
            {!collapsed ? (
              <div className="space-y-2">
                <div className="flex items-center gap-2 px-2">
                  <div className="w-8 h-8 bg-blue-500 rounded-full flex items-center justify-center text-xs font-bold flex-shrink-0 text-white">
                    {user?.username?.[0]?.toUpperCase()||'U'}
                  </div>
                  <p className="text-xs text-white/70 truncate flex-1">{user?.username||'مستخدم'}</p>
                </div>
                <button onClick={()=>setShowChangePass(true)} className="w-full text-right text-xs text-white/50 hover:text-white px-2 py-1 rounded transition-colors">🔑 كلمة المرور</button>
                <button onClick={handleLogout}   className="w-full text-right text-xs text-white/50 hover:text-white px-2 py-1 rounded transition-colors">↩ خروج</button>
              </div>
            ) : (
              <button onClick={handleLogout} className="w-full flex justify-center text-white/50 hover:text-white text-xl py-1">↩</button>
            )}
          </div>
        </aside>

        {/* ── Main content area ── */}
        <div className="flex-1 flex flex-col min-w-0 overflow-hidden">

          {/* ── Top header ── */}
          <header className="h-14 bg-white border-b border-gray-200 flex items-center px-4 gap-3 flex-shrink-0 z-30">
            {/* Mobile: hamburger */}
            <button onClick={()=>setMobileMenu(true)} className="md:hidden text-gray-500 hover:text-gray-800 w-9 h-9 flex items-center justify-center rounded-lg hover:bg-gray-100">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16"/>
              </svg>
            </button>
            {/* Desktop: collapse toggle */}
            <button onClick={()=>setCollapsed(!collapsed)} className="hidden md:flex text-gray-500 hover:text-gray-800 w-9 h-9 items-center justify-center rounded-lg hover:bg-gray-100">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16"/>
              </svg>
            </button>

            {/* Page title (mobile) */}
            <div className="flex-1 md:hidden">
              <p className="text-sm font-bold text-gray-800 truncate">
                {FULL_NAV.find(n=>location.pathname.startsWith(n.to))?.label || 'دارة الملك عبدالعزيز'}
              </p>
            </div>
            <div className="hidden md:flex flex-1"/>

            {/* Search shortcut */}
            <button onClick={()=>navigate('/search')}
              className="hidden sm:flex text-gray-500 hover:text-gray-800 w-9 h-9 items-center justify-center rounded-lg hover:bg-gray-100 transition-colors">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
              </svg>
            </button>
            {/* Notifications */}
            <button onClick={()=>setNotifOpen(p=>!p)}
              className="relative text-gray-500 hover:text-gray-800 w-9 h-9 flex items-center justify-center rounded-lg hover:bg-gray-100 transition-colors">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/>
              </svg>
              {unreadCount > 0 && (
                <span className="absolute -top-0.5 -left-0.5 w-4 h-4 bg-red-500 text-white text-[10px] rounded-full flex items-center justify-center font-bold">
                  {unreadCount}
                </span>
              )}
            </button>

            {/* Avatar */}
            <div className="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center text-white text-sm font-bold flex-shrink-0">
              {user?.username?.[0]?.toUpperCase() || 'U'}
            </div>
          </header>

          {/* ── Page content ── */}
          <main className="flex-1 overflow-y-auto p-4 md:p-6 pb-20 md:pb-6">
            <Outlet />
          </main>
        </div>
      </div>

      {/* ── Mobile bottom navigation ── */}
      <nav className="md:hidden fixed bottom-0 inset-x-0 z-30 bg-white border-t border-gray-200 flex items-stretch" dir="rtl"
        style={{boxShadow:'0 -4px 20px rgba(0,0,0,0.08)'}}>
        {BOTTOM_NAV.map(item => (
          <NavLink key={item.to} to={item.to}
            className={({isActive}) =>
              `flex-1 flex flex-col items-center justify-center py-2 px-1 text-center transition-colors min-w-0 ${
                isActive ? 'text-blue-700' : 'text-gray-400'
              }`}>
            {({isActive}) => (
              <>
                <span className={`text-xl mb-0.5 transition-transform ${isActive?'scale-110':''}`}>{item.icon}</span>
                <span className={`text-[9px] font-bold truncate w-full text-center ${isActive?'text-blue-700':''}`}>{item.label}</span>
                {isActive && <div className="w-4 h-0.5 bg-blue-700 rounded-full mt-0.5"/>}
              </>
            )}
          </NavLink>
        ))}
      </nav>
    </>
  )
}
