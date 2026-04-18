import { demoLinearProject, demoLinearTasks, demoNotifications } from '../demoData';

// Re-export types to match the real module shape.
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
  isPaused: boolean;
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

export interface UpdateTaskRequest {
  title: string;
  description?: string | null;
  assignedUserId?: string | null;
  priority?: string;
  dueDate?: string | null;
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

let tasks: Task[] = [...demoLinearTasks];
let notifications: NotificationItem[] = [...demoNotifications];

export async function listProjects(_activeOnly: boolean = true, _mineOnly: boolean = true): Promise<ListProjectsResponse> {
  return { projects: [demoLinearProject], totalCount: 1 };
}

export async function getProject(id: string): Promise<ProjectItem> {
  if (id === demoLinearProject.id) return demoLinearProject;
  return { ...demoLinearProject, id, name: 'Demo Project' };
}

export async function listProjectTasks(projectId: string): Promise<ListTasksResponse> {
  const t = tasks.filter((x) => x.projectId === projectId);
  return {
    tasks: t,
    todoCount: t.filter((x) => x.status === 'Todo').length,
    inProgressCount: t.filter((x) => x.status === 'InProgress' || x.status === 'InReview').length,
    doneCount: t.filter((x) => x.status === 'Done').length,
    totalSecondsWorked: t.reduce((sum, x) => sum + (x.totalSecondsWorked ?? 0), 0),
  };
}

export async function listProjectMembers(_projectId: string): Promise<ListMembersResponse> {
  return { members: [], totalCount: 0 };
}

export async function addProjectMember(_projectId: string, _userId: string, _role: string = 'member'): Promise<ProjectMember> {
  return {
    id: `pm-${Date.now()}`,
    projectId: demoLinearProject.id,
    userId: _userId,
    displayName: 'Demo Member',
    email: 'demo@chronosx.app',
    role: _role,
    addedAt: new Date().toISOString(),
  };
}

export async function listUserTasks(_userId: string | undefined, includeDone: boolean = false): Promise<ListTasksResponse> {
  const t = includeDone ? tasks : tasks.filter((x) => x.status !== 'Done');
  return {
    tasks: t,
    todoCount: t.filter((x) => x.status === 'Todo').length,
    inProgressCount: t.filter((x) => x.status === 'InProgress' || x.status === 'InReview').length,
    doneCount: t.filter((x) => x.status === 'Done').length,
    totalSecondsWorked: t.reduce((sum, x) => sum + (x.totalSecondsWorked ?? 0), 0),
  };
}

export async function listMyTasks(includeDone: boolean = false): Promise<ListTasksResponse> {
  return listUserTasks('u-demo', includeDone);
}

export async function createTask(projectId: string, body: CreateTaskRequest): Promise<Task> {
  const now = new Date().toISOString();
  const id = `t-${Math.floor(Math.random() * 1_000_000)}`;
  const next: Task = {
    id,
    projectId,
    projectName: demoLinearProject.name,
    projectColor: demoLinearProject.color,
    title: body.title,
    description: body.description ?? null,
    status: 'Todo',
    createdByUserId: 'u-demo',
    assignedUserId: 'u-demo',
    assignedUserDisplayName: 'Demo User',
    priority: (body.priority as any) ?? 'Medium',
    dueDate: body.dueDate ?? null,
    position: 9999,
    createdAt: now,
    updatedAt: null,
    movedToInProgressAt: null,
    completedAt: null,
    totalSecondsWorked: 0,
    rowVersion: 1,
    isRunning: false,
    isPaused: false,
    runningSeconds: null,
    isLinearSourced: false,
    linearIssueIdentifier: null,
    linearUrl: null,
    linearStateName: null,
  };
  tasks = [...tasks, next];
  return next;
}

export async function deleteTask(id: string): Promise<void> {
  tasks = tasks.filter((t) => t.id !== id);
}

export async function moveTask(id: string, body: MoveTaskRequest): Promise<Task> {
  const idx = tasks.findIndex((t) => t.id === id);
  if (idx < 0) throw new Error('Not found');
  const prev = tasks[idx];
  const next: Task = {
    ...prev,
    status: body.status,
    position: body.position ?? prev.position,
    rowVersion: (prev.rowVersion ?? 0) + 1,
  };
  tasks = [...tasks.slice(0, idx), next, ...tasks.slice(idx + 1)];
  return next;
}

export async function updateTask(id: string, body: UpdateTaskRequest): Promise<Task> {
  const idx = tasks.findIndex((t) => t.id === id);
  if (idx < 0) throw new Error('Not found');
  const prev = tasks[idx];
  const next: Task = {
    ...prev,
    title: body.title ?? prev.title,
    description: body.description ?? prev.description,
    assignedUserId: body.assignedUserId ?? prev.assignedUserId,
    priority: (body.priority as any) ?? prev.priority,
    dueDate: body.dueDate ?? prev.dueDate,
    updatedAt: new Date().toISOString(),
    rowVersion: (prev.rowVersion ?? 0) + 1,
  };
  tasks = [...tasks.slice(0, idx), next, ...tasks.slice(idx + 1)];
  return next;
}

export async function getTask(id: string): Promise<Task> {
  const t = tasks.find((x) => x.id === id);
  if (!t) throw new Error('Not found');
  return t;
}

export async function getMyOpenTask(): Promise<OpenTaskResponse | null> {
  const inProgress = tasks.find((t) => t.status === 'InProgress');
  if (!inProgress) return null;
  return {
    taskId: inProgress.id,
    projectId: inProgress.projectId,
    projectName: inProgress.projectName,
    projectColor: inProgress.projectColor,
    taskTitle: inProgress.title,
    startedAt: inProgress.movedToInProgressAt ?? inProgress.createdAt,
    pausedSeconds: 0,
    isPaused: false,
    elapsedSeconds: Math.max(60, inProgress.totalSecondsWorked),
  };
}

export async function pauseMyOpenTask(): Promise<void> {}
export async function resumeMyOpenTask(): Promise<void> {}
export async function closeMyOpenTaskOnIdleReject(): Promise<void> {}

export async function getMyTaskEntries(_date: string): Promise<ListTaskEntriesResponse> {
  return {
    entries: [
      {
        id: 'te-1',
        taskTitle: 'Implement waitlist API route',
        projectName: demoLinearProject.name,
        projectColor: demoLinearProject.color,
        startedAt: new Date(Date.now() - 58 * 60_000).toISOString(),
        endedAt: new Date(Date.now() - 22 * 60_000).toISOString(),
      },
      {
        id: 'te-2',
        taskTitle: 'Polish dashboard cards',
        projectName: demoLinearProject.name,
        projectColor: demoLinearProject.color,
        startedAt: new Date(Date.now() - 20 * 60_000).toISOString(),
        endedAt: null,
      },
    ],
  };
}

export async function listMyNotifications(_unreadOnly: boolean = false, take: number = 50): Promise<ListNotificationsResponse> {
  const n = notifications.slice(0, take);
  const unreadCount = n.filter((x) => !x.readAt).length;
  return { notifications: n, unreadCount, totalCount: n.length };
}

export async function markNotificationRead(id: string): Promise<void> {
  notifications = notifications.map((n) => (n.id === id ? { ...n, readAt: new Date().toISOString() } : n));
}

export async function markAllNotificationsRead(): Promise<void> {
  const now = new Date().toISOString();
  notifications = notifications.map((n) => ({ ...n, readAt: n.readAt ?? now }));
}

export async function removeProjectMember(_projectId: string, _userId: string): Promise<void> {}
