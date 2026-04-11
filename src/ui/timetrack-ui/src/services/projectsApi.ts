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

export type ProjectSyncSource = 'Local' | 'Linear';

export interface ProjectItem {
  id: string;
  name: string;
  description: string | null;
  color: string;
  status: string;
  createdAt: string;
  updatedAt: string | null;
  isBillable: boolean;
  currency: string | null;
  hourlyRate: number | null;
  syncSource: ProjectSyncSource;
  linearProjectId: string | null;
  lastSyncedAt: string | null;
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

export type TaskStatus = 'Todo' | 'InProgress' | 'InReview' | 'Done';
export type TaskPriority = 'None' | 'Low' | 'Medium' | 'High' | 'Urgent';

export interface Task {
  id: string;
  projectId: string;
  projectName: string;
  projectColor: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  createdByUserId: string;
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
  isLinearSourced: boolean;
  linearIssueIdentifier: string | null;
  linearUrl: string | null;
  linearStateName: string | null;
}

export interface CreateTaskRequest {
  title: string;
  description?: string | null;
  priority?: string;
  dueDate?: string | null;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string | null;
  assignedUserId?: string | null;
  priority?: string;
  dueDate?: string | null;
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

export function getProject(id: string): Promise<ProjectItem> {
  return api.get<ProjectItem>(`/projects/${encodeURIComponent(id)}`);
}

export function listProjectTasks(projectId: string): Promise<ListTasksResponse> {
  return api.get<ListTasksResponse>(`/projects/${encodeURIComponent(projectId)}/tasks`);
}

export function listProjectMembers(projectId: string): Promise<ListMembersResponse> {
  return api.get<ListMembersResponse>(`/projects/${encodeURIComponent(projectId)}/members`);
}

export function createTask(projectId: string, body: CreateTaskRequest): Promise<Task> {
  return api.post<Task>(`/projects/${encodeURIComponent(projectId)}/tasks`, body);
}

export function updateTask(id: string, body: UpdateTaskRequest): Promise<Task> {
  return api.put<Task>(`/tasks/${encodeURIComponent(id)}`, body);
}

export function deleteTask(id: string): Promise<void> {
  return api.delete<void>(`/tasks/${encodeURIComponent(id)}`);
}

export function moveTask(id: string, body: MoveTaskRequest): Promise<Task> {
  return api.patch<Task>(`/tasks/${encodeURIComponent(id)}/move`, body);
}

export function getTask(id: string): Promise<Task> {
  return api.get<Task>(`/tasks/${encodeURIComponent(id)}`);
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

// ============================================================================
// TYPES — Task Entries (for activity timeline)
// ============================================================================

export interface TaskEntryDto {
  id: string;
  taskTitle: string;
  projectName: string;
  projectColor: string;
  startedAt: string;
  endedAt: string | null;
}

export interface ListTaskEntriesResponse {
  entries: TaskEntryDto[];
}

export function getMyTaskEntries(date: string): Promise<ListTaskEntriesResponse> {
  return api.get<ListTaskEntriesResponse>(`/me/task-entries?date=${encodeURIComponent(date)}`);
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
