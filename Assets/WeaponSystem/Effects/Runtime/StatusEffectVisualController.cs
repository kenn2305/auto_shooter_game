using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StatusEffectController))]
public sealed class StatusEffectVisualController : MonoBehaviour
{
    private readonly Dictionary<StatusEffectDefinition, ParticleSystem> activeVisuals = new();

    private static Material sharedParticleMaterial;

    private StatusEffectController statusEffects;

    private void Awake()
    {
        statusEffects = GetComponent<StatusEffectController>();
    }

    private void OnEnable()
    {
        statusEffects.StatusApplied += HandleStatusApplied;
        statusEffects.StatusRemoved += HandleStatusRemoved;
    }

    private void OnDisable()
    {
        if (statusEffects != null)
        {
            statusEffects.StatusApplied -= HandleStatusApplied;
            statusEffects.StatusRemoved -= HandleStatusRemoved;
        }

        ClearVisuals();
    }

    private void HandleStatusApplied(ActiveStatusEffect status)
    {
        StatusEffectDefinition definition = status.Definition;
        if (definition.VisualStyle == StatusEffectVisualStyle.None)
        {
            return;
        }

        if (!activeVisuals.TryGetValue(definition, out ParticleSystem particles))
        {
            particles = CreateVisual(definition);
            activeVisuals.Add(definition, particles);
        }

        ParticleStyle style = GetStyle(definition.VisualStyle);
        ParticleSystem.EmissionModule emission = particles.emission;
        float stackRatio = definition.MaximumStacks <= 1
            ? 0f
            : (status.Stacks - 1f) / (definition.MaximumStacks - 1f);
        emission.rateOverTime = style.EmissionRate * Mathf.Lerp(1f, 1.75f, stackRatio);

        if (!particles.isPlaying)
        {
            particles.Play(true);
        }

        particles.Emit(Mathf.Clamp(3 + status.Stacks * 2, 4, 12));
    }

    private void HandleStatusRemoved(StatusEffectDefinition definition)
    {
        if (statusEffects.HasStatus(definition))
        {
            return;
        }

        if (!activeVisuals.TryGetValue(definition, out ParticleSystem particles))
        {
            return;
        }

        activeVisuals.Remove(definition);
        Destroy(particles.gameObject);
    }

    private ParticleSystem CreateVisual(StatusEffectDefinition definition)
    {
        GameObject visualObject = new($"StatusVFX_{definition.StatusId}");
        Transform visualTransform = visualObject.transform;
        visualTransform.SetParent(transform, false);
        visualTransform.localPosition = definition.VisualOffset;
        visualTransform.localScale = Vector3.one * definition.VisualScale;

        ParticleSystem particles = visualObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(particles, definition);
        return particles;
    }

    private void ConfigureParticleSystem(
        ParticleSystem particles,
        StatusEffectDefinition definition
    )
    {
        ParticleStyle style = GetStyle(definition.VisualStyle);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 96;
        main.startLifetime = style.Lifetime;
        main.startSpeed = 0f;
        main.startSize = style.StartSize;
        main.startColor = definition.VisualColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = style.EmissionRate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = style.Radius;
        shape.radiusThickness = style.RadiusThickness;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(
            style.VelocityYMin,
            style.VelocityYMax
        );
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = CreateFadeGradient();

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = GetSharedParticleMaterial();

        SpriteRenderer enemyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyRenderer != null)
        {
            particleRenderer.sortingLayerID = enemyRenderer.sortingLayerID;
            particleRenderer.sortingOrder = enemyRenderer.sortingOrder + 20;
        }
        else
        {
            particleRenderer.sortingOrder = 20;
        }
    }

    private static Material GetSharedParticleMaterial()
    {
        if (sharedParticleMaterial != null)
        {
            return sharedParticleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        sharedParticleMaterial = new Material(shader)
        {
            name = "Status Effect Particle Material (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        return sharedParticleMaterial;
    }

    private static ParticleSystem.MinMaxGradient CreateFadeGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0.85f, 0.65f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static ParticleStyle GetStyle(StatusEffectVisualStyle style)
    {
        return style switch
        {
            StatusEffectVisualStyle.Mark => new ParticleStyle(
                lifetime: 1.15f,
                startSize: 0.12f,
                emissionRate: 12f,
                radius: 0.58f,
                radiusThickness: 1f,
                velocityYMin: -0.04f,
                velocityYMax: 0.04f
            ),
            StatusEffectVisualStyle.Slow => new ParticleStyle(
                lifetime: 1.3f,
                startSize: 0.1f,
                emissionRate: 9f,
                radius: 0.5f,
                radiusThickness: 1f,
                velocityYMin: -0.32f,
                velocityYMax: -0.12f
            ),
            StatusEffectVisualStyle.Stun => new ParticleStyle(
                lifetime: 0.75f,
                startSize: 0.15f,
                emissionRate: 11f,
                radius: 0.42f,
                radiusThickness: 1f,
                velocityYMin: 0.02f,
                velocityYMax: 0.1f
            ),
            StatusEffectVisualStyle.Burn => new ParticleStyle(
                lifetime: 0.65f,
                startSize: 0.16f,
                emissionRate: 18f,
                radius: 0.34f,
                radiusThickness: 0.65f,
                velocityYMin: 0.55f,
                velocityYMax: 1.05f
            ),
            StatusEffectVisualStyle.Poison => new ParticleStyle(
                lifetime: 1.05f,
                startSize: 0.14f,
                emissionRate: 10f,
                radius: 0.38f,
                radiusThickness: 0.8f,
                velocityYMin: 0.2f,
                velocityYMax: 0.5f
            ),
            _ => new ParticleStyle(
                lifetime: 1f,
                startSize: 0.12f,
                emissionRate: 8f,
                radius: 0.4f,
                radiusThickness: 1f,
                velocityYMin: 0f,
                velocityYMax: 0f
            ),
        };
    }

    private void ClearVisuals()
    {
        foreach (ParticleSystem particles in activeVisuals.Values)
        {
            if (particles != null)
            {
                Destroy(particles.gameObject);
            }
        }

        activeVisuals.Clear();
    }

    private readonly struct ParticleStyle
    {
        public float Lifetime { get; }
        public float StartSize { get; }
        public float EmissionRate { get; }
        public float Radius { get; }
        public float RadiusThickness { get; }
        public float VelocityYMin { get; }
        public float VelocityYMax { get; }

        public ParticleStyle(
            float lifetime,
            float startSize,
            float emissionRate,
            float radius,
            float radiusThickness,
            float velocityYMin,
            float velocityYMax
        )
        {
            Lifetime = lifetime;
            StartSize = startSize;
            EmissionRate = emissionRate;
            Radius = radius;
            RadiusThickness = radiusThickness;
            VelocityYMin = velocityYMin;
            VelocityYMax = velocityYMax;
        }
    }
}
