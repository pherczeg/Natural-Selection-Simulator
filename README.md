# Natural Selection Simulator

A Unity-based simulation that demonstrates the principles of natural selection and evolution through autonomous creatures with genetic traits.

## 🎯 Overview

This project simulates a population of herbivorous creatures (and predators) that evolve over time through natural selection. Each creature has genetic traits including weight, speed, sense radius, and energy levels. Through reproduction with genetic mutation, successful traits are passed on while unsuccessful ones are eliminated, demonstrating Darwin's theory of evolution in action.

## ✨ Features

### Core Simulation

- **Autonomous Creatures**: Herbivores with complex state-based behavior systems
- **Genetic Evolution**: Traits are inherited with mutation during reproduction
- **Natural Selection**: Creatures with better traits survive and reproduce more successfully
- **Statistical Tracking**: Real-time data collection and CSV export for analysis

### Creature Behaviors

- **State Machine System**: Creatures have multiple behavioral states:
  - Idle, Wandering, Searching for Food/Mates
  - Moving to Food/Mates, Eating, Reproducing
- **Energy Management**: Creatures consume energy and must eat to survive
- **Age System**: Creatures age over time and die of old age
- **Reproduction**: Mating system with cooldowns and energy requirements

### Genetic Traits

- **Weight**: Affects energy consumption
- **Speed**: Movement rate across the environment
- **Sense Radius**: Detection range for food and mates
- **Energy Capacity**: Maximum energy storage

### Environment

- **Food Spawning**: Dynamic food generation across the terrain
- **Terrain System**: 3D environment with ground boundaries
- **Object Pooling**: Efficient memory management for creatures and food

## 🚀 Getting Started

### Prerequisites

- Unity 2022.3 LTS or newer
- Windows, macOS, or Linux

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/pherczeg/Natural-Selection-Simulator.git
   ```
2. Open the project in Unity
3. Load the `First.unity` scene from `Assets/Scenes/`
4. Press Play to start the simulation

### Configuration

Modify simulation parameters in the `GameConfig` asset located at `Assets/Resources/GameConfig.asset`:

- **Population Settings**: Initial creature count, predator count
- **Genetics**: Mutation rate, mutation chance
- **Energy System**: Max energy, consumption rates, thresholds
- **Reproduction**: Cooldown times, energy requirements, minimum age
- **Simulation**: Update intervals, restart time, number of simulations

## 🎮 How It Works

### Simulation Flow

1. **Initialization**: Creatures spawn with random genetic traits
2. **Behavior Loop**: Each creature follows its state machine:
   - Search for food when energy is low
   - Look for mates when ready to reproduce
   - Wander when needs are met
3. **Selection Pressure**: Creatures die from:
   - Energy depletion (starvation)
   - Old age
   - Predation (if predators are enabled)
4. **Reproduction**: Successful creatures mate and produce offspring with inherited traits
5. **Evolution**: Over generations, beneficial traits become more common

### Genetic System

- Traits are inherited from one parent (50% chance from each)
- Mutations occur based on configurable chance and rate
- Offspring traits are constrained by minimum values
- Natural selection favors:
  - Efficient energy usage (optimal weight)
  - Effective foraging (good sense radius)
  - Balanced speed (fast enough to find resources, not too energy-costly)

## 📊 Data Collection

The simulation automatically exports statistics to CSV files including:

- Population size over time
- Average genetic traits (weight, speed, sense radius)
- Energy and age statistics
- Generational data for evolutionary analysis

## 🏗️ Project Structure

```
Assets/
├── Materials/          # Visual materials for creatures and environment
├── Prefabs/           # Creature and food prefabs
├── Resources/         # Game configuration
├── Scenes/            # Unity scenes
├── Scripts/
│   ├── Creature/      # Creature behavior system
│   │   ├── CreatureBehaviour/    # Base creature logic
│   │   ├── States/               # Behavior state machine
│   │   ├── AgeManagement/        # Age system
│   │   ├── EnergyManagement/     # Energy system
│   │   ├── Movement/             # Movement system
│   │   ├── Observation/          # Sensing system
│   │   └── Reproduction/         # Mating and genetics
│   ├── Jobs/          # Unity Job System for performance
│   └── Spawners/      # Creature and food spawning
└── Shaders/           # Custom shaders
```

## 🔧 Customization

### Adding New Behaviors

1. Create new state classes inheriting from `CreatureStateBase`
2. Add state transitions in creature behavior classes
3. Update the `CreatureStateType` enum

### Modifying Genetics

- Edit trait inheritance in `ReproductionManager.cs`
- Adjust mutation parameters in `GameConfig`
- Add new traits by extending the creature initialization system

### Environmental Changes

- Modify terrain in the Unity scene
- Adjust food spawning parameters in `FoodSpawner.cs`
- Change creature spawn locations in `CreatureSpawner.cs`

## 📈 Research Applications

This simulator can be used for:

- **Educational Purposes**: Teaching evolution and natural selection
- **Research**: Studying genetic algorithms and emergent behavior
- **Game Development**: Base for evolution-based game mechanics
- **Data Science**: Generating datasets for machine learning

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🔗 Links

- **Repository**: [https://github.com/pherczeg/Natural-Selection-Simulator](https://github.com/pherczeg/Natural-Selection-Simulator)
- **Unity**: [https://unity.com/](https://unity.com/)

## 📞 Contact

Peter Herczeg - [@pherczeg](https://github.com/pherczeg)

Project Link: [https://github.com/pherczeg/Natural-Selection-Simulator](https://github.com/pherczeg/Natural-Selection-Simulator)
