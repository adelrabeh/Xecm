import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { LangProvider } from './i18n.js'

// Clear old mock data when version changes
const DATA_VERSION = '2.0'
if (localStorage.getItem('ecm_data_version') !== DATA_VERSION) {
  const KEEP = ['ecm_lang', 'ecm_token', 'ecm_user', 'ecm_users']
  Object.keys(localStorage)
    .filter(k => !KEEP.includes(k))
    .forEach(k => localStorage.removeItem(k))
  localStorage.setItem('ecm_data_version', DATA_VERSION)
}
import './index.css'

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode><LangProvider><App /></LangProvider></React.StrictMode>
)
