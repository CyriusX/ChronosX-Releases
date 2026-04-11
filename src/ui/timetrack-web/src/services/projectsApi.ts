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
  status: string; // 'Active' | 'Archived'
  createdAt: string;
  updatedAt: string | null;
  syncSource: ProjectSyncSource;
  linearProjectId: string | null;
  lastSyncedAt: string | null;
}

export interface ListProjectsResponse {
  projects: ProjectItem[];
  totalCount: number;
}

export interface CreateProjectRequest {
  name: string;
  description?: string;
  color?: string;
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
  role: string; // 'Member' | 'Owner'
  addedAt: string;
}

export interface ListMembersResponse {
  members: ProjectMember[];
  totalCount: number;
}

export interface AddMemberRequest {
  userId: string;
  role?: string;
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

export interface ListTasksResponse {
  tasks: Task[];
  todoCount: number;
  inProgressCount: number;
  doneCount: number;
  totalSecondsWorked: number;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  assignedUserId?: string | null;
  priority?: TaskPriority;
  dueDate?: string | null;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  assignedUserId?: string | null;
  priority?: TaskPriority;
  dueDate?: string | null;
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
  kind: string; // 'TaskAssigned' | 'TaskUnassigned' | 'TaskUpdated' | 'ProjectMembershipChanged' | 'Generic'
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
// API — Projects
// ============================================================================

export function listProjects(activeOnly: boolean = true): Promise<ListProjectsResponse> {
  return api.get<ListProjectsResponse>(`/projects?activeOnly=${activeOnly}`);
}

export function getProject(id: string): Promise<ProjectItem> {
  return api.get<ProjectItem>(`/projects/${encodeURIComponent(id)}`);
}

export function createProject(body: CreateProjectRequest): Promise<ProjectItem> {
  return api.post<ProjectItem>('/projects', body);
}

export function updateProject(id: string, body: CreateProjectRequest): Promise<ProjectItem> {
  return api.put<ProjectItem>(`/projects/${encodeURIComponent(id)}`, body);
}

export function archiveProject(id: string): Promise<void> {
  return api.post<void>(`/projects/${encodeURIComponent(id)}/archive`);
}

export function reactivateProject(id: string): Promise<void> {
  return api.post<void>(`/projects/${encodeURIComponent(id)}/reactivate`);
}

export function deleteProject(id: string): Promise<void> {
  return api.delete<void>(`/projects/${encodeURIComponent(id)}`);
}

// ============================================================================
// API — Members
// ============================================================================

export function listProjectMembers(projectId: string): Promise<ListMembersResponse> {
  return api.get<ListMembersResponse>(`/projects/${encodeURIComponent(projectId)}/members`);
}

export function addProjectMember(projectId: string, body: AddMemberRequest): Promise<ProjectMember> {
  return api.post<ProjectMember>(`/projects/${encodeURIComponent(projectId)}/members`, body);
}

export function removeProjectMember(projectId: string, userId: string): Promise<void> {
  return api.delete<void>(`/projects/${encodeURIComponent(projectId)}/members/${encodeURIComponent(userId)}`);
}

// ============================================================================
// API — Tasks
// ============================================================================

export function listProjectTasks(projectId: string): Promise<ListTasksResponse> {
  return api.get<ListTasksResponse>(`/projects/${encodeURIComponent(projectId)}/tasks`);
}

export function createTask(projectId: string, body: CreateTaskRequest): Promise<Task> {
  return api.post<Task>(`/projects/${encodeURIComponent(projectId)}/tasks`, body);
}

export function updateTask(id: string, body: UpdateTaskRequest): Promise<Task> {
  return api.put<Task>(`/tasks/${encodeURIComponent(id)}`, body);
}

export function moveTask(id: string, body: MoveTaskRequest): Promise<Task> {
  return api.patch<Task>(`/tasks/${encodeURIComponent(id)}/move`, body);
}

export function getTask(id: string): Promise<Task> {
  return api.get<Task>(`/tasks/${encodeURIComponent(id)}`);
}

export function deleteTask(id: string): Promise<void> {
  return api.delete<void>(`/tasks/${encodeURIComponent(id)}`);
}

export function listMyTasks(includeDone: boolean = false): Promise<ListTasksResponse> {
  return api.get<ListTasksResponse>(`/me/tasks?includeDone=${includeDone}`);
}

export function getMyOpenTask(): Promise<OpenTaskResponse | null> {
  return api.get<OpenTaskResponse | null>('/me/tasks/open');
}

// ============================================================================
// API — Notifications
// ============================================================================

export function listMyNotifications(unreadOnly: boolean = false, take: number = 50): Promise<ListNotificationsResponse> {
  return api.get<ListNotificationsResponse>(`/me/notifications?unreadOnly=${unreadOnly}&take=${take}`);
}

export function markNotificationRead(id: string): Promise<void> {
  return api.post<void>(`/me/notifications/${encodeURIComponent(id)}/read`);
}

export function markAllNotificationsRead(): Promise<void> {
  return api.post<void>('/me/notifications/read-all');
}
