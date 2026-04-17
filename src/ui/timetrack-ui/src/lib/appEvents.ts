import type { Task, ProjectItem } from '../services/projectsApi';
import type { Project } from '../stores/projectStore';

export const APP_EVENTS = {
  taskUpdated: 'timetrack:taskUpdated',
  taskDeleted: 'timetrack:taskDeleted',
  projectUpdated: 'timetrack:projectUpdated',
} as const;

export function emitTaskUpdated(task: Task): void {
  window.dispatchEvent(new CustomEvent<Task>(APP_EVENTS.taskUpdated, { detail: task }));
}

export function onTaskUpdated(handler: (task: Task) => void): () => void {
  const listener = (e: Event) => handler((e as CustomEvent<Task>).detail);
  window.addEventListener(APP_EVENTS.taskUpdated, listener);
  return () => window.removeEventListener(APP_EVENTS.taskUpdated, listener);
}

export function emitTaskDeleted(taskId: string): void {
  window.dispatchEvent(new CustomEvent<string>(APP_EVENTS.taskDeleted, { detail: taskId }));
}

export function onTaskDeleted(handler: (taskId: string) => void): () => void {
  const listener = (e: Event) => handler((e as CustomEvent<string>).detail);
  window.addEventListener(APP_EVENTS.taskDeleted, listener);
  return () => window.removeEventListener(APP_EVENTS.taskDeleted, listener);
}

export function emitProjectUpdated(project: Project | ProjectItem): void {
  window.dispatchEvent(new CustomEvent<Project | ProjectItem>(APP_EVENTS.projectUpdated, { detail: project }));
}

export function onProjectUpdated(handler: (project: Project | ProjectItem) => void): () => void {
  const listener = (e: Event) => handler((e as CustomEvent<Project | ProjectItem>).detail);
  window.addEventListener(APP_EVENTS.projectUpdated, listener);
  return () => window.removeEventListener(APP_EVENTS.projectUpdated, listener);
}

