import { useState, useEffect, useCallback } from 'react'
import { useLocalStorage } from './useLocalStorage'

// Seed notifications for demo
const SEED = [] // cleared — real notifications only
const _SEED_BAK = [
  { id:1,  type:'task_assigned',  titleAr:'مهمة جديدة',              titleEn:'New Task',               bodyAr:'تم تكليفك بمهمة: مراجعة عقود الموردين', bodyEn:'You have been assigned: Vendor Contracts Review', navigateTo:'/tasks', isRead:false, createdAt:'2026-04-25T10:30:00Z' },
  { id:2,  type:'task_overdue',   titleAr:'مهمة متأخرة',             titleEn:'Task Overdue',           bodyAr:'المهمة "تدقيق المشتريات" تجاوزت الموعد', bodyEn:'"Procurement Audit" is overdue', navigateTo:'/tasks', isRead:false, createdAt:'2026-04-25T09:00:00Z' },
  { id:3,  type:'record_locked',  titleAr:'سجل مُقفَل',              titleEn:'Record Locked',          bodyAr:'تم اعتماد وقفل السجل DARAH-LEG-2026-11234', bodyEn:'Record DARAH-LEG-2026-11234 approved and locked', navigateTo:'/records', isRead:false, createdAt:'2026-04-24T15:00:00Z' },
  { id:4,  type:'escalation',     titleAr:'تصعيد جديد',              titleEn:'New Escalation',         bodyAr:'تلقيت تصعيداً من خالد القحطاني', bodyEn:'Escalation received from Khalid Al-Qahtani', navigateTo:'/tasks', isRead:true, createdAt:'2026-04-24T11:00:00Z' },
  { id:5,  type:'document_shared',titleAr:'وثيقة مُشارَكة',          titleEn:'Document Shared',        bodyAr:'شارك معك أحمد الزهراني: تقرير الميزانية', bodyEn:'Ahmed Al-Zahrani shared: Budget Report', navigateTo:'/documents', isRead:true, createdAt:'2026-04-23T13:00:00Z' },
  { id:6,  type:'comment',        titleAr:'تعليق جديد',              titleEn:'New Comment',            bodyAr:'تعليق جديد على مهمتك: إعداد تقرير الأداء', bodyEn:'New comment on: Performance Report', navigateTo:'/tasks', isRead:true, createdAt:'2026-04-22T10:00:00Z' },
]

const TYPE_ICON = {
  task_assigned:'📋', task_overdue:'⚠️', record_locked:'🔐',
  escalation:'🔺', document_shared:'📄', comment:'💬',
  default:'🔔'
}

export function useNotifications() {
  const [notifs, setNotifs] = useLocalStorage('ecm_notifications', SEED)
  const [connected, setConnected] = useState(false)
  const safe = Array.isArray(notifs) ? notifs : SEED
  const unread = safe.filter(n => !n.isRead).length

  // Try SignalR connection
  useEffect(() => {
    let conn = null
    const connect = async () => {
      try {
        const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr')
        const token = localStorage.getItem('ecm_token')
        if (!token) return

        conn = new HubConnectionBuilder()
          .withUrl('https://xecm-production.up.railway.app/hubs/notifications', {
            accessTokenFactory: () => token
          })
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Warning)
          .build()

        conn.on('Notification', (payload) => {
          setNotifs(prev => {
            const arr = Array.isArray(prev) ? prev : SEED
            return [{ ...payload, isRead: false }, ...arr].slice(0, 50)
          })
        })

        await conn.start()
        setConnected(true)
      } catch {
        setConnected(false) // Silent fallback to polling/mock
      }
    }
    connect()
    return () => { conn?.stop() }
  }, [])

  const markRead = useCallback((id) => {
    setNotifs(prev => (Array.isArray(prev)?prev:SEED).map(n => n.id===id ? {...n,isRead:true} : n))
  }, [])

  const markAllRead = useCallback(() => {
    setNotifs(prev => (Array.isArray(prev)?prev:SEED).map(n => ({...n,isRead:true})))
  }, [])

  const addNotification = useCallback((notif) => {
    setNotifs(prev => [notif, ...(Array.isArray(prev)?prev:SEED)].slice(0,50))
  }, [])

  return { notifications: safe, unread, connected, markRead, markAllRead, addNotification, TYPE_ICON }
}
