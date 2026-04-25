import React from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useAuthStore } from './store/authStore'
import LoginPage from './auth/LoginPage'
import AppLayout from './layout/AppLayout'
import DashboardPage from './modules/dashboard/DashboardPage'
import DocumentsPage from './modules/documents/DocumentsPage'
import WorkflowsPage from './modules/workflows/WorkflowsPage'
import TasksPage from './modules/tasks/TasksPage'
import SearchPage from './modules/search/SearchPage'
import { RecordsPage, AdminPage } from './modules/other/OtherPages'
import LibraryPage from './modules/library/LibraryPage'
import ContentModelPage from './modules/contentmodel/ContentModelPage'

function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuthStore()
  return isAuthenticated() ? children : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="documents" element={<DocumentsPage />} />
          <Route path="workflows" element={<WorkflowsPage />} />
          <Route path="tasks" element={<TasksPage />} />
          <Route path="search" element={<SearchPage />} />
          <Route path="records" element={<RecordsPage />} />
          <Route path="library" element={<LibraryPage />} />
          <Route path="content-model" element={<ContentModelPage />} />
          <Route path="admin" element={<AdminPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
