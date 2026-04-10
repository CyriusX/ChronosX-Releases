/**
 * Projects API — Desktop version.
 *
 * Backend endpoints for kanban tasks, notifications, and member listings.
 * Typed wrappers mirror the web app's projectsApi.ts for consistency.
 */

import { api } from './apiClient';

// ============================================================================
// TYPES — Projects
// ============================================================================

export interface ProjectItem {
  id: string;
  name: string;
  description: string | null;
  color: string;
  status: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface ListProjectsResponse {
  projects: ProjectItem[];
  totalCount: number;
}

// ============================================================================
// TYPES — Members
// ============================================================================

export interface ProjectMember {
  id: string;
  projectId: string;
  userId: string;
  displayName: string;
  email: string;
  role: string;
  addedAt: string;
}

export interface ListMembersResponse {
  members: ProjectMember[];
  totalCount: number;
}

// ============================================================================
// TYPES — Tasks
// ============================================================================

export type TaskStatus = 'Todo' | 'InProgress' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High';

export interface Task {
  id: string;
  projectId: string;
  projectName: string;
  projectColor: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  assignedUserId: string | null;
  assignedUserDisplayName: string | null;
  priority: TaskPriority;
  dueDate: string | null;
  position: number;
  createdAt: string;
  updatedAt: string | null;
  movedToInProgressAt: string | null;
  completedAt: string | null;
  totalSecondsWorked: number;
  rowVersion: number;
  isRunning: boolean;
  runningSeconds: number | null;
}

export interface ListTasksResponse {
  tasks: Task[];
  todoCount: number;
  inProgressCount: number;
  doneCount: number;
  totalSecondsWorked: number;
}

export interface MoveTaskRequest {
  status: TaskStatus;
  position?: number;
  rowVersion?: number;
}

export interface OpenTaskResponse {
  taskId: string;
  projectId: string;
  projectName: string;
  projectColor: string;
  taskTitle: string;
  startedAt: string;
  pausedSeconds: number;
  isPaused: boolean;
  elapsedSeconds: number;
}

// ============================================================================
// TYPES — Notifications
// ============================================================================

export interface NotificationItem {
  id: string;
  kind: string;
  title: string;
  body: string;
  metadataJson: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface ListNotificationsResponse {
  notifications: NotificationItem[];
  unreadCount: number;
  totalCount: number;
}

// ============================================================================
// API
// ============================================================================

export function listProjects(activeOnly: boolean = true): Promise<ListProjectsResponse> {
  return api.get<ListProjectsResponse>(`/projects?activeOnly=${activeOnly}`);
}

export function listProjectTasks(projectId: string): Promise<ListTasksResponse> {
  return api.get<ListTasksResponse>(`/projects/${encodeURIComponent(projectId)}/tasks`);
}

export function listProjectMembers(projectId: string): Promise<ListMembersResponse> {
  return api.get<ListMembersResponse>(`/projects/${encodeURIComponent(projectId)}/members`);
}

export function moveTask(id: string, body: MoveTaskRequest): Promise<Task> {
  return api.patch<Task>(`/tasks/${encodeURIComponent(id)}/move`, body);
}

export function listMyTasks(includeDone: boolean = false): Promise<ListTasksResponse> {
  return api.get<ListTasksResponse>(`/me/tasks?includeDone=${includeDone}`);
}

export function getMyOpenTask(): Promise<OpenTaskResponse | null> {
  return api.get<OpenTaskResponse | null>('/me/tasks/open');
}

export function pauseMyOpenTask(): Promise<void> {
  return api.post<void>('/me/tasks/open/pause');
}

export function resumeMyOpenTask(): Promise<void> {
  return api.post<void>('/me/tasks/open/resume');
}

export function closeMyOpenTaskOnIdleReject(): Promise<void> {
  return api.post<void>('/me/tasks/open/close-on-idle-reject');
}

export function listMyNotifications(unreadOnly: boolean = false, take: number = 50): Promise<ListNotificationsResponse> {
  return api.get<ListNotificationsResponse>(`/me/notifications?unreadOnly=${unreadOnly}&take=${take}`);
}

export function markNotificationRead(id: string): Promise<void> {
  return api.post<void>(`/me/notifications/${encodeURIComponent(id)}/read`);
}

export function markAllNotificationsRead(): Promise<void> {
  return api.post<void>('/me/notifications/read-all');
}
