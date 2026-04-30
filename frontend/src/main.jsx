import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import { LangProvider } from './i18n.js'

// Clear old mock data when version changes
const DATA_VERSION = '2.0'
if (localStorage.getItem('ecm_data_version') !== DATA_VERSION) {
  const KEEP = ['ecm_lang', 'ecm_token', 'ecm_user', 'ecm_users', 'ecm_data_version', 'ecm_tasks_v2', 'ecm_folder_tree', 'ecm_library_files_v2', 'ecm_records', 'ecm_docs', 'ecm_notifications', 'ecm_search_history']
  Object.keys(localStorage)
    .filter(k => !KEEP.includes(k))
    .forEach(k => localStorage.removeItem(k))
  localStorage.setItem('ecm_data_version', DATA_VERSION)
}
import './index.css'

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode><LangProvider><App /></LangProvider></React.StrictMode>
)
