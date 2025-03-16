# VisualEQ to EQEmu Spawn Editor Implementation Plan

This document outlines the plan for transforming the VisualEQ client into a visual editor for managing EQEmu zone spawn data. The editor will allow users to connect to an EQEmu database, load zones with their associated NPC spawns, and visually manipulate spawn locations and properties in a 3D environment.

## Project Vision

Create a user-friendly 3D visual editor that allows EQEmu server administrators to:
- Visualize all NPC spawn locations in a zone
- Drag and drop NPCs to position them precisely within the game world
- Edit spawn properties, groups, and paths
- Save changes directly to the EQEmu database

## Prerequisites

- A functioning VisualEQ client that can load zones and character models
- Access to an EQEmu database with spawn data
- Understanding of the EQEmu database schema related to spawns

## Phase 1: Research and Foundation

### Character Model Loading
1. Thoroughly test the existing character loading capabilities
   - Experiment with different character models
   - Document which models/races work correctly
   - Identify any limitations in the current implementation

### Database Connectivity
1. Create a database access layer
   - Implement connection to MySQL/MariaDB
   - Create configuration for storing connection details
   - Test basic connectivity and queries

### EQEmu Data Structure Analysis
1. Analyze the EQEmu database schema for spawn-related tables:
   - `spawn2` - Spawn point locations
   - `spawnentry` - Which NPCs can spawn at a given spawn group
   - `spawngroup` - Groups of spawn entries
   - `npc_types` - NPC definitions
   - Any other relevant tables (spawn conditions, spawn times, etc.)

2. Map database entities to code objects for:
   - Spawn points
   - NPC definitions
   - Spawn groups
   - Other related entities

## Phase 2: Core Functionality

### Data Retrieval
1. Implement zone-specific data loading:
   - Query spawn points for the currently loaded zone
   - Retrieve NPC data associated with those spawn points
   - Load relevant spawn group information

### Visual Representation
1. Extend `LoadCharacter` method to support loading multiple character models
   - Map NPC types to character models
   - Handle cases where models aren't available

2. Implement spawn point visualization:
   - Place character models at their spawn coordinates
   - Add visual indicators for spawn points
   - Implement distinguishing features for different spawn types

### Basic UI Elements
1. Create UI for database connection settings
2. Implement a simple spawn list/browser
3. Add basic information display for selected spawns

## Phase 3: Editing Capabilities

### Selection System
1. Implement object selection in the 3D view
   - Ray casting for clicking on models
   - Visual indication of selected objects
   - Multi-selection capability

### Transform Controls
1. Create manipulation controls for spawn positioning:
   - Drag handles for X/Y/Z movement
   - Visual grid or snapping system
   - Coordinate display and manual entry

### Property Editing
1. Build a property panel for editing spawn attributes:
   - Position (X, Y, Z coordinates)
   - Orientation (heading)
   - Spawn group assignments
   - Respawn times
   - Spawn conditions

### Path Editing
1. Implement visualization and editing of NPC movement paths:
   - Display waypoints and paths
   - Add/remove/move waypoints
   - Set waypoint properties (pause time, etc.)

## Phase 4: Advanced Features

### Grouping Operations
1. Add tools for working with multiple spawns:
   - Clone/duplicate spawns
   - Align spawns (horizontal/vertical)
   - Distribute spawns evenly
   - Group/ungroup spawns

### Search and Filter
1. Implement search capabilities:
   - Find spawns by NPC name/ID
   - Filter by spawn type, level, or other attributes
   - Highlight matching spawns in the 3D view

### Import/Export
1. Create functionality to:
   - Export spawn configurations to files
   - Import spawn data from files
   - Support for partial imports (e.g., just a group of spawns)

### Persistent History
1. Implement undo/redo system:
   - Track changes to spawn data
   - Allow reverting to previous states
   - Maintain history between sessions

## Phase 5: Refinement and Integration

### Database Writing
1. Implement save functionality:
   - Validation of changes before saving
   - Transaction support for atomic updates
   - Conflict resolution for multi-user scenarios

### User Experience Improvements
1. Add helpful features:
   - Keyboard shortcuts
   - Context menus
   - Status indicators
   - Progress reporting for long operations

### Performance Optimization
1. Improve handling of large zones:
   - Level of detail for distant models
   - Culling of off-screen objects
   - Optimization of database queries

### Documentation and Help
1. Create user documentation:
   - Quick start guide
   - Tutorial for common tasks
   - Keyboard shortcut reference
   - Troubleshooting guide

## Technical Considerations

### Database Access
- Use a dedicated data access layer that abstracts database operations
- Consider supporting multiple database types (MySQL, MariaDB, SQLite)
- Implement connection pooling for performance

### 3D Manipulation
- Implement proper world-to-screen coordinate conversion
- Handle camera perspective when placing objects
- Support both precise and freeform placement

### Multi-threading
- Keep UI responsive during database operations
- Load models asynchronously
- Consider background saving of changes

### Configuration
- Store database connection details securely
- Allow customization of UI and behavior
- Support profiles for different server setups

## Development Approach

1. **Incremental Development**:
   - Start with a minimal viable product that can display spawns
   - Add editing capabilities progressively
   - Release early versions for testing and feedback

2. **Modularity**:
   - Separate database code from UI code
   - Use interfaces for swappable components
   - Allow for future extensions

3. **Testing**:
   - Create a test database for development
   - Develop automated tests for critical functionality
   - Plan for user testing sessions

## Resources Needed

1. **Development Environment**:
   - .NET SDK
   - MySQL/MariaDB instance with EQEmu schema
   - Test data for different zone types

2. **Documentation**:
   - EQEmu database schema documentation
   - VisualEQ code documentation
   - 3D manipulation references

3. **Tools**:
   - Database management tool
   - Version control system
   - Issue tracking for feature development

## Success Criteria

The project will be considered successful when:

1. Users can connect to an EQEmu database and load any zone
2. All spawn points in a zone are correctly visualized
3. Users can select, move, and edit spawn properties
4. Changes can be saved back to the database
5. The tool is stable and performs well with large zones

## Future Enhancements

After the core functionality is complete, consider these extensions:

1. **Door and Object Editing**: Extend to support placement of doors and objects
2. **Zone Transition Editing**: Allow editing of zone transition points
3. **Terrain Visualization**: Show ground spawns vs. elevated positions clearly
4. **Multi-User Collaboration**: Support for multiple editors working simultaneously
5. **Scripting Support**: Allow for automation of common tasks via scripts
6. **Advanced Filtering**: Visual heatmaps of spawn density, level ranges, etc.

---

This implementation plan serves as a roadmap for development and can be adjusted as the project progresses and requirements evolve. 