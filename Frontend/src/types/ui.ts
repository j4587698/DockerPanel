// ==================== UI组件相关类型定义 ====================

import { BaseConfig, Status, Priority } from './common'

// UI基础配置
export interface UIConfig {
  theme: UITheme
  layout: UILayout
  navigation: UINavigation
  components: UIComponents
  branding: UIBranding
  accessibility: UIAccessibility
  responsive: UIResponsive
  animations: UIAnimations
  notifications: UINotifications
  localization: UILocalization
  performance: UIPerformance
}

// UI主题
export interface UITheme {
  mode: 'light' | 'dark' | 'auto'
  primary: UIColorPalette
  secondary: UIColorPalette
  success: UIColorPalette
  warning: UIColorPalette
  error: UIColorPalette
  info: UIColorPalette
  neutral: UIColorPalette
  background: UIColorPalette
  text: UIColorPalette
  border: UIColorPalette
  shadow: UIShadowPalette
  spacing: UISpacingScale
  typography: UITypographyScale
  breakpoints: UIBreakpointScale
  elevation: UIElevationScale
  custom: Record<string, any>
}

export interface UIColorPalette {
  50: string
  100: string
  200: string
  300: string
  400: string
  500: string
  600: string
  700: string
  800: string
  900: string
  950: string
}

export interface UIShadowPalette {
  none: string
  sm: string
  default: string
  md: string
  lg: string
  xl: string
  '2xl': string
  inner: string
}

export interface UISpacingScale {
  0: string
  1: string
  2: string
  3: string
  4: string
  5: string
  6: string
  8: string
  10: string
  12: string
  16: string
  20: string
  24: string
  32: string
  40: string
  48: string
  56: string
  64: string
  80: string
  96: string
  112: string
  128: string
  144: string
  160: string
  176: string
  192: string
  208: string
  224: string
  240: string
  256: string
  288: string
  320: string
  384: string
  448: string
  512: string
  576: string
  640: string
  720: string
  768: string
  832: string
  896: string
  960: string
  1024: string
  1088: string
  1152: string
  1216: string
  1280: string
  1344: string
  1408: string
  1472: string
  1536: string
  1600: string
  1664: string
  1728: string
  1792: string
  1856: string
  1920: string
  1984: string
  2048: string
}

export interface UITypographyScale {
  xs: UITypography
  sm: UITypography
  base: UITypography
  lg: UITypography
  xl: UITypography
  '2xl': UITypography
  '3xl': UITypography
  '4xl': UITypography
  '5xl': UITypography
  '6xl': UITypography
  '7xl': UITypography
  '8xl': UITypography
  '9xl': UITypography
}

export interface UITypography {
  fontSize: string
  fontWeight: string
  lineHeight: string
  letterSpacing: string
}

export interface UIBreakpointScale {
  sm: string
  md: string
  lg: string
  xl: string
  '2xl': string
}

export interface UIElevationScale {
  none: string
  sm: string
  md: string
  lg: string
  xl: string
  '2xl': string
}

// UI布局
export interface UILayout {
  type: 'default' | 'compact' | 'spacious' | 'sidebar' | 'topbar' | 'mobile'
  header: UILayoutHeader
  sidebar: UILayoutSidebar
  footer: UILayoutFooter
  content: UILayoutContent
  breakpoints: UILayoutBreakpoints
  grid: UILayoutGrid
  container: UILayoutContainer
}

export interface UILayoutHeader {
  height: string
  fixed: boolean
  border: boolean
  shadow: boolean
  background: string
  navigation: boolean
  user: boolean
  breadcrumbs: boolean
  actions: boolean
  logo: boolean
  title: boolean
  search: boolean
  notifications: boolean
  darkMode: boolean
}

export interface UILayoutSidebar {
  width: string
  collapsedWidth: string
  collapsible: boolean
  fixed: boolean
  border: boolean
  shadow: boolean
  background: string
  navigation: boolean
  user: boolean
  logo: boolean
  search: boolean
  footer: boolean
  autoCollapse: boolean
  hoverExpand: boolean
}

export interface UILayoutFooter {
  height: string
  fixed: boolean
  border: boolean
  background: string
  copyright: boolean
  links: boolean
  version: boolean
}

export interface UILayoutContent {
  padding: string
  maxWidth: string
  centered: boolean
  background: string
  scrollable: boolean
}

export interface UILayoutBreakpoints {
  mobile: string
  tablet: string
  desktop: string
  wide: string
}

export interface UILayoutGrid {
  columns: number
  gap: string
  responsive: boolean
}

export interface UILayoutContainer {
  maxWidth: string
  centered: boolean
  padding: string
}

// UI导航
export interface UINavigation {
  type: 'sidebar' | 'topbar' | 'breadcrumb' | 'tabs' | 'menu'
  items: UINavigationItem[]
  active: string
  expanded: string[]
  searchable: boolean
  collapsible: boolean
  icons: boolean
  badges: boolean
  tooltips: boolean
}

export interface UINavigationItem {
  id: string
  label: string
  icon?: string
  badge?: number
  tooltip?: string
  href?: string
  target?: string
  children?: UINavigationItem[]
  disabled?: boolean
  hidden?: boolean
  external?: boolean
  badgeType?: 'default' | 'primary' | 'success' | 'warning' | 'error'
  badgeColor?: string
  onClick?: () => void
}

// UI组件
export interface UIComponents {
  button: UIComponentButton
  input: UIComponentInput
  form: UIComponentForm
  table: UIComponentTable
  card: UIComponentCard
  modal: UIComponentModal
  dropdown: UIComponentDropdown
  tabs: UIComponentTabs
  accordion: UIComponentAccordion
  tree: UIComponentTree
  upload: UIComponentUpload
  pagination: UIComponentPagination
  breadcrumb: UIComponentBreadcrumb
  steps: UIComponentSteps
  timeline: UIComponentTimeline
  progress: UIComponentProgress
  rate: UIComponentRate
  slider: UIComponentSlider
  switch: UIComponentSwitch
  checkbox: UIComponentCheckbox
  radio: UIComponentRadio
  select: UIComponentSelect
  datePicker: UIComponentDatePicker
  timePicker: UIComponentTimePicker
  colorPicker: UIComponentColorPicker
  editor: UIComponentEditor
  chart: UIComponentChart
  kanban: UIComponentKanban
  calendar: UIComponentCalendar
  fileExplorer: UIComponentFileExplorer
  terminal: UIComponentTerminal
  codeEditor: UIComponentCodeEditor
  logViewer: UIComponentLogViewer
  dashboard: UIComponentDashboard
}

export interface UIComponentButton {
  variant: 'solid' | 'outline' | 'ghost' | 'link'
  size: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
  color: 'primary' | 'secondary' | 'success' | 'warning' | 'error' | 'info' | 'neutral'
  rounded: boolean
  shadow: boolean
  loading: boolean
  disabled: boolean
  icon: boolean
  block: boolean
  group: boolean
}

export interface UIComponentInput {
  variant: 'outline' | 'filled' | 'flushed' | 'unstyled'
  size: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
  placeholder: string
  disabled: boolean
  readonly: boolean
  required: boolean
  error: boolean
  clearable: boolean
  prefix: boolean
  suffix: boolean
  addon: boolean
}

export interface UIComponentForm {
  layout: 'vertical' | 'horizontal' | 'inline'
  labelPosition: 'top' | 'left' | 'right'
  required: boolean
  disabled: boolean
  readonly: boolean
  validation: UIComponentFormValidation
  submitText: string
  cancelText: string
  resetText: string
}

export interface UIComponentFormValidation {
  validateOnChange: boolean
  showErrors: boolean
  realTime: boolean
  debounce: number
}

export interface UIComponentTable {
  size: 'sm' | 'md' | 'lg'
  striped: boolean
  bordered: boolean
  hoverable: boolean
  sticky: boolean
  scrollable: boolean
  pagination: boolean
  selection: boolean
  sorting: boolean
  filtering: boolean
  expandable: boolean
  actions: boolean
}

export interface UIComponentCard {
  variant: 'default' | 'outlined' | 'elevated'
  padding: 'none' | 'sm' | 'md' | 'lg' | 'xl'
  shadow: boolean
  border: boolean
  hoverable: boolean
  clickable: boolean
}

export interface UIComponentModal {
  size: 'sm' | 'md' | 'lg' | 'xl' | 'full'
  centered: boolean
  closable: boolean
  mask: boolean
  maskClosable: boolean
  keyboard: boolean
  draggable: boolean
  footer: boolean
  header: boolean
}

export interface UIComponentDropdown {
  trigger: 'click' | 'hover' | 'focus'
  placement: 'top' | 'bottom' | 'left' | 'right' | 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right'
  arrow: boolean
  offset: number
  delay: number
  searchable: boolean
  multiSelect: boolean
}

export interface UIComponentTabs {
  type: 'line' | 'card' | 'pills'
  size: 'sm' | 'md' | 'lg'
  centered: boolean
  addable: boolean
  closable: boolean
  animated: boolean
  inkBar: boolean
}

export interface UIComponentAccordion {
  bordered: boolean
  ghost: boolean
  collapsible: boolean
  expandIcon: boolean
  showArrow: boolean
  defaultActiveKey: string[]
}

export interface UIComponentTree {
  checkable: boolean
  selectable: boolean
  draggable: boolean
  virtual: boolean
  height: number
  showLine: boolean
  showIcon: boolean
}

export interface UIComponentUpload {
  type: 'button' | 'drag' | 'card'
  multiple: boolean
  directory: boolean
  accept: string
  maxSize: number
  maxCount: number
  preview: boolean
  list: boolean
  drag: boolean
}

export interface UIComponentPagination {
  size: 'sm' | 'md' | 'lg'
  showSizeChanger: boolean
  showQuickJumper: boolean
  showTotal: boolean
  simple: boolean
  pageSize: number
  total: number
  current: number
}

export interface UIComponentBreadcrumb {
  separator: string
  showIcon: boolean
}

export interface UIComponentSteps {
  direction: 'horizontal' | 'vertical'
  size: 'sm' | 'md' | 'lg'
  labelPlacement: 'horizontal' | 'vertical'
  progressDot: boolean
  current: number
  status: 'wait' | 'process' | 'finish' | 'error'
}

export interface UIComponentTimeline {
  mode: 'left' | 'alternate' | 'right'
  pending: boolean
  reverse: boolean
}

export interface UIComponentProgress {
  type: 'line' | 'circle' | 'dashboard'
  size: 'sm' | 'md' | 'lg'
  percent: number
  status: 'normal' | 'success' | 'exception' | 'active'
  showInfo: boolean
  strokeColor: string
  trailColor: string
}

export interface UIComponentRate {
  count: number
  allowHalf: boolean
  allowClear: boolean
  disabled: boolean
  character: string
  tooltips: string[]
}

export interface UIComponentSlider {
  range: boolean
  vertical: boolean
  marks: Record<number, string>
  step: number
  min: number
  max: number
  disabled: boolean
  tooltip: boolean
}

export interface UIComponentSwitch {
  size: 'sm' | 'md' | 'lg'
  loading: boolean
  disabled: boolean
}

export interface UIComponentCheckbox {
  indeterminate: boolean
  disabled: boolean
}

export interface UIComponentRadio {
  button: boolean
  disabled: boolean
}

export interface UIComponentSelect {
  mode: 'single' | 'multiple' | 'tags'
  allowClear: boolean
  showSearch: boolean
  filterOption: boolean
  maxTagCount: number
  disabled: boolean
  loading: boolean
}

export interface UIComponentDatePicker {
  format: string
  showTime: boolean
  showToday: boolean
  disabled: boolean
  allowClear: boolean
  inputReadOnly: boolean
}

export interface UIComponentTimePicker {
  format: string
  hourStep: number
  minuteStep: number
  secondStep: number
  disabled: boolean
  use12Hours: boolean
}

export interface UIComponentColorPicker {
  format: 'hex' | 'hsl' | 'rgb' | 'hsb'
  showText: boolean
  allowClear: boolean
  disabled: boolean
}

export interface UIComponentEditor {
  mode: 'wysiwyg' | 'markdown' | 'code'
  toolbar: string[]
  height: number
  maxHeight: number
  autoSave: boolean
  preview: boolean
}

export interface UIComponentChart {
  type: 'line' | 'bar' | 'pie' | 'area' | 'scatter' | 'radar' | 'gauge' | 'funnel' | 'treemap'
  responsive: boolean
  legend: boolean
  tooltip: boolean
  animation: boolean
  height: number
  data: any[]
}

export interface UIComponentKanban {
  columns: UIKanbanColumn[]
  draggable: boolean
  searchable: boolean
  filterable: boolean
  groupable: boolean
}

export interface UIKanbanColumn {
  id: string
  title: string
  cards: UIKanbanCard[]
  color: string
  width: number
}

export interface UIKanbanCard {
  id: string
  title: string
  description: string
  tags: string[]
  assignee: string
  priority: string
  dueDate: string
  attachments: number
}

export interface UIComponentCalendar {
  mode: 'month' | 'year' | 'decade'
  selectMode: 'single' | 'multiple' | 'range'
  showToday: boolean
  showWeekNumbers: boolean
  showWeekend: boolean
  disabled: boolean
}

export interface UIComponentFileExplorer {
  view: 'list' | 'grid' | 'tree'
  showHidden: boolean
  sortBy: string
  sortOrder: 'asc' | 'desc'
  selectable: boolean
  multiSelect: boolean
  draggable: boolean
  searchable: boolean
  filterable: boolean
}

export interface UIComponentTerminal {
  theme: 'light' | 'dark' | 'auto'
  fontSize: number
  fontFamily: string
  cursorBlink: boolean
  scrollback: number
  copyOnSelect: boolean
  allowPaste: boolean
  bellStyle: string
  visualBell: boolean
}

export interface UIComponentCodeEditor {
  language: string
  theme: string
  fontSize: number
  tabSize: number
  wordWrap: boolean
  lineNumbers: boolean
  minimap: boolean
  folding: boolean
  autoComplete: boolean
  snippets: boolean
  formatOnSave: boolean
}

export interface UIComponentLogViewer {
  level: 'debug' | 'info' | 'warn' | 'error' | 'fatal'
  autoScroll: boolean
  follow: boolean
  wrap: boolean
  search: boolean
  filter: boolean
  export: boolean
  clear: boolean
  maxLines: number
}

export interface UIComponentDashboard {
  layout: 'grid' | 'flex'
  columns: number
  gap: string
  widgets: UIDashboardWidget[]
  draggable: boolean
  resizable: boolean
  collapsible: boolean
}

export interface UIDashboardWidget {
  id: string
  type: string
  title: string
  size: UIDashboardWidgetSize
  position: UIDashboardWidgetPosition
  config: Record<string, any>
  data: any
}

export interface UIDashboardWidgetSize {
  width: number
  height: number
}

export interface UIDashboardWidgetPosition {
  x: number
  y: number
}

// UI品牌
export interface UIBranding {
  name: string
  logo: string
  logoDark: string
  favicon: string
  title: string
  description: string
  version: string
  copyright: string
  author: string
  url: string
  email: string
  colors: UIBrandingColors
  fonts: UIBrandingFonts
}

export interface UIBrandingColors {
  primary: string
  secondary: string
  accent: string
  background: string
  text: string
  link: string
  success: string
  warning: string
  error: string
  info: string
}

export interface UIBrandingFonts {
  heading: string
  body: string
  mono: string
}

// UI可访问性
export interface UIAccessibility {
  enabled: boolean
  highContrast: boolean
  reducedMotion: boolean
  screenReader: boolean
  keyboard: boolean
  focus: boolean
  colorBlindness: string
  fontSize: 'xs' | 'sm' | 'md' | 'lg' | 'xl'
  lineHeight: 'tight' | 'normal' | 'relaxed'
  spacing: 'compact' | 'normal' | 'spacious'
  skipLinks: UISkipLink[]
  ariaLabels: UIAriaLabel[]
  announcements: UIAnnouncement[]
}

export interface UISkipLink {
  id: string
  label: string
  target: string
}

export interface UIAriaLabel {
  id: string
  label: string
}

export interface UIAnnouncement {
  id: string
  message: string
  type: 'polite' | 'assertive'
  timeout: number
}

// UI响应式
export interface UIResponsive {
  enabled: boolean
  breakpoints: UIResponsiveBreakpoints
  grid: UIResponsiveGrid
  typography: UIResponsiveTypography
  spacing: UIResponsiveSpacing
  visibility: UIResponsiveVisibility
}

export interface UIResponsiveBreakpoints {
  xs: number
  sm: number
  md: number
  lg: number
  xl: number
  '2xl': number
}

export interface UIResponsiveGrid {
  columns: Record<string, number>
  gap: Record<string, string>
}

export interface UIResponsiveTypography {
  scale: Record<string, number>
}

export interface UIResponsiveSpacing {
  scale: Record<string, number>
}

export interface UIResponsiveVisibility {
  hidden: Record<string, boolean>
  visible: Record<string, boolean>
}

// UI动画
export interface UIAnimations {
  enabled: boolean
  duration: UIAnimationDuration
  easing: UIAnimationEasing
  transitions: UIAnimationTransition[]
  keyframes: UIAnimationKeyframe[]
}

export interface UIAnimationDuration {
  fast: string
  normal: string
  slow: string
}

export interface UIAnimationEasing {
  ease: string
  easeIn: string
  easeOut: string
  easeInOut: string
}

export interface UIAnimationTransition {
  name: string
  duration: string
  easing: string
  delay: string
}

export interface UIAnimationKeyframe {
  name: string
  keyframes: Record<string, any>
}

// UI通知
export interface UINotifications {
  enabled: boolean
  position: 'top' | 'bottom' | 'left' | 'right' | 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right'
  maxCount: number
  duration: number
  pauseOnHover: boolean
  showProgress: boolean
  closable: boolean
  sounds: boolean
  vibration: boolean
}

// 本地化
export interface UILocalization {
  enabled: boolean
  language: string
  fallback: string
  autoDetect: boolean
  rtl: boolean
  dateFormat: string
  timeFormat: string
  numberFormat: string
  currency: string
  translations: Record<string, Record<string, string>>
}

// UI性能
export interface UIPerformance {
  enabled: boolean
  optimization: UIPerformanceOptimization
  monitoring: UIPerformanceMonitoring
  metrics: UIPerformanceMetrics
}

export interface UIPerformanceOptimization {
  virtualScroll: boolean
  lazyLoading: boolean
  imageOptimization: boolean
  bundleSplitting: boolean
  caching: boolean
  preloading: boolean
}

export interface UIPerformanceMonitoring {
  enabled: boolean
  webVitals: boolean
  renderTime: boolean
  bundleSize: boolean
  memory: boolean
  network: boolean
}

export interface UIPerformanceMetrics {
  fcp: number
  lcp: number
  cls: number
  ttfb: number
  renderTime: number
  bundleSize: number
  memoryUsage: number
  networkRequests: number
}

// 事件系统
export interface UIEvent {
  type: string
  source: string
  payload: any
  timestamp: string
  id: string
}

export interface UIEventHandler {
  type: string
  handler: (event: UIEvent) => void
  once?: boolean
  passive?: boolean
}

export interface UIEventBus {
  on: (type: string, handler: UIEventHandler) => void
  off: (type: string, handler: UIEventHandler) => void
  emit: (type: string, payload: any) => void
  once: (type: string, handler: UIEventHandler) => void
}

// 状态管理
export interface UIState {
  theme: UITheme
  layout: UILayout
  navigation: UINavigation
  notifications: UINotificationsState
  loading: UILoadingState
  error: UIErrorState
  user: UIUserState
  preferences: UIPreferencesState
}

export interface UINotificationsState {
  items: UINotificationItem[]
  count: number
}

export interface UINotificationItem {
  id: string
  type: 'success' | 'error' | 'warning' | 'info'
  title: string
  message: string
  duration: number
  timestamp: string
  read: boolean
  actions: UINotificationAction[]
}

export interface UINotificationAction {
  label: string
  action: () => void
  primary?: boolean
}

export interface UILoadingState {
  global: boolean
  components: Record<string, boolean>
}

export interface UIErrorState {
  global: string | null
  components: Record<string, string>
}

export interface UIUserState {
  user: any
  authenticated: boolean
  permissions: string[]
  preferences: UIPreferencesState
}

export interface UIPreferencesState {
  theme: string
  language: string
  layout: string
  notifications: boolean
  sounds: boolean
  animations: boolean
}

// 组件状态
export interface UIComponentState {
  loading: boolean
  error: string | null
  data: any
  dirty: boolean
  touched: boolean
  valid: boolean
  disabled: boolean
  readonly: boolean
}

export interface UIFormState {
  values: Record<string, any>
  errors: Record<string, string[]>
  touched: Record<string, boolean>
  dirty: Record<string, boolean>
  valid: boolean
  submitting: boolean
}

export interface UITableState {
  data: any[]
  loading: boolean
  error: string | null
  pagination: UIPaginationState
  sorting: UISortingState
  filtering: UIFilteringState
  selection: UISelectionState
}

export interface UIPaginationState {
  current: number
  pageSize: number
  total: number
  showSizeChanger: boolean
  showQuickJumper: boolean
}

export interface UISortingState {
  field: string
  direction: 'asc' | 'desc'
}

export interface UIFilteringState {
  filters: Record<string, any>
  active: boolean
}

export interface UISelectionState {
  selected: any[]
  selectedKeys: string[]
  type: 'single' | 'multiple'
}

// 工具函数
export interface UIUtils {
  debounce: (func: Function, wait: number) => Function
  throttle: (func: Function, wait: number) => Function
  deepClone: <T>(obj: T) => T
  deepMerge: <T extends object>(target: T, source: Partial<T>) => T
  formatBytes: (bytes: number) => string
  formatNumber: (num: number, options?: Intl.NumberFormatOptions) => string
  formatDate: (date: Date, format?: string) => string
  generateId: () => string
  validateEmail: (email: string) => boolean
  validatePhone: (phone: string) => boolean
  validateUrl: (url: string) => boolean
  truncate: (text: string, length: number) => string
  capitalize: (text: string) => string
  slugify: (text: string) => string
  pluralize: (count: number, word: string) => string
}

// Hook 类型
export interface UIHook {
  useState: <T>(initial: T) => [T, (value: T) => void]
  useEffect: (effect: () => void | (() => void), deps?: any[]) => void
  useLayoutEffect: (effect: () => void | (() => void), deps?: any[]) => void
  useRef: <T>(initial: T) => React.MutableRefObject<T>
  useCallback: <T extends (...args: any[]) => any>(callback: T, deps: any[]) => T
  useMemo: <T>(factory: () => T, deps: any[]) => T
  useContext: <T>(context: React.Context<T>) => T
  useReducer: <S, A>(reducer: React.Reducer<S, A>, initialState: S) => [S, React.Dispatch<A>]
}

// 组件属性类型
export interface UIComponentProps {
  className?: string
  style?: React.CSSProperties
  children?: React.ReactNode
  id?: string
  testId?: string
  'aria-label'?: string
  'aria-describedby'?: string
  'aria-labelledby'?: string
}

export interface UIComponentWithChildrenProps extends UIComponentProps {
  children: React.ReactNode
}

export interface UIComponentWithClassNameProps extends UIComponentProps {
  className: string
}

export interface UIComponentWithStyleProps extends UIComponentProps {
  style: React.CSSProperties
}

// 自定义组件类型
export interface UICustomComponent {
  name: string
  displayName: string
  props: Record<string, any>
  defaultProps: Record<string, any>
  propTypes: Record<string, any>
  contextTypes?: Record<string, any>
  childContextTypes?: Record<string, any>
}

// 插件系统
export interface UIPlugin {
  name: string
  version: string
  description: string
  author: string
  license: string
  dependencies: string[]
  install: () => void
  uninstall: () => void
  configure: (config: any) => void
}

export interface UIPluginManager {
  register: (plugin: UIPlugin) => void
  unregister: (name: string) => void
  get: (name: string) => UIPlugin | undefined
  list: () => UIPlugin[]
  configure: (name: string, config: any) => void
}

// 主题提供者
export interface UIThemeProvider {
  theme: UITheme
  setTheme: (theme: Partial<UITheme>) => void
  resetTheme: () => void
  toggleMode: () => void
}

// 响应式钩子
export interface UIResponsiveHook {
  breakpoint: string
  isMobile: boolean
  isTablet: boolean
  isDesktop: boolean
  isWide: boolean
  width: number
  height: number
}

// 本地化钩子
export interface UILocalizationHook {
  language: string
  t: (key: string, params?: Record<string, any>) => string
  formatDate: (date: Date, format?: string) => string
  formatNumber: (num: number, options?: Intl.NumberFormatOptions) => string
  formatCurrency: (num: number, currency?: string) => string
}

// 通知钩子
export interface UINotificationHook {
  show: (notification: Partial<UINotificationItem>) => void
  success: (title: string, message?: string, duration?: number) => void
  error: (title: string, message?: string, duration?: number) => void
  warning: (title: string, message?: string, duration?: number) => void
  info: (title: string, message?: string, duration?: number) => void
  dismiss: (id: string) => void
  clear: () => void
}

// 加载钩子
export interface UILoadingHook {
  loading: boolean
  show: () => void
  hide: () => void
  toggle: () => void
}

// 错误钩子
export interface UIErrorHook {
  error: string | null
  setError: (error: string | null) => void
  clear: () => void
  show: (error: string, duration?: number) => void
}

// 用户钩子
export interface UIUserHook {
  user: any
  authenticated: boolean
  permissions: string[]
  login: (credentials: any) => Promise<void>
  logout: () => Promise<void>
  updateProfile: (profile: any) => Promise<void>
  updatePreferences: (preferences: any) => Promise<void>
}